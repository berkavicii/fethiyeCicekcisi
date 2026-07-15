using KedWear.Core.Entities;
using KedWear.Core.Enums;
using KedWear.Core.Interfaces.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KedWear.Infrastructure.Data.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            // Migrate is for a real (relational) provider like Postgres — EnsureCreated is
            // the zero-setup path for the InMemory provider, which doesn't support migrations
            // at all and throws if you call MigrateAsync against it.
            if (context.Database.IsRelational())
                await context.Database.MigrateAsync();
            else
                await context.Database.EnsureCreatedAsync();

            await SeedRolesAsync(roleManager);
            await SeedAdminUserAsync(userManager);
            await SeedCategoriesAsync(context);
            await SeedRealProductsFromFolderAsync(scope.ServiceProvider, context, logger);
            logger.LogInformation("Seed data başarıyla yüklendi.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Seed data yüklenirken hata oluştu.");
        }
    }

    /// <summary>Reads src/KedWear.Web/seed-images/urunler.txt (a simple hand-editable manifest)
    /// and, for each block, imports the product's photos from its sibling folder and creates a
    /// real Product + ProductImage rows — the "launch with my actual catalog already loaded"
    /// path, distinct from the fake Shopier-CDN demo products above. Runs every startup and is
    /// idempotent (skips products that already exist by name), so re-running after adding more
    /// entries to the manifest picks up only the new ones.</summary>
    private static async Task SeedRealProductsFromFolderAsync(IServiceProvider services, AppDbContext context, ILogger logger)
    {
        var env = services.GetRequiredService<IHostEnvironment>();
        var fileService = services.GetRequiredService<IFileService>();

        var seedRoot = Path.Combine(env.ContentRootPath, "seed-images");
        var manifestPath = Path.Combine(seedRoot, "urunler.txt");
        if (!File.Exists(manifestPath)) return;

        var entries = ParseManifest(await File.ReadAllTextAsync(manifestPath));
        if (entries.Count == 0) return;

        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

        foreach (var entry in entries)
        {
            if (!entry.TryGetValue("ad", out var name) || string.IsNullOrWhiteSpace(name))
            {
                logger.LogWarning("seed-images/urunler.txt: 'ad' alanı olmayan bir blok atlandı.");
                continue;
            }

            var existing = await context.Products.Include(p => p.Variants)
                                                 .FirstOrDefaultAsync(p => p.Name == name);
            if (existing is not null)
            {
                // Already seeded on a previous run. Stock lines may have been added to the
                // manifest after the product was first imported (the field is newer than some
                // entries), so backfill variants once — but never overwrite live stock counts,
                // which the admin may have adjusted since.
                if (existing.Variants.Count == 0)
                {
                    var backfill = ParseVariants(entry);
                    if (backfill.Count > 0)
                    {
                        foreach (var v in backfill) existing.Variants.Add(v);
                        await context.SaveChangesAsync();
                        logger.LogInformation("seed-images/urunler.txt: '{Name}' için stok/beden bilgisi sonradan eklendi.", name);
                    }
                }
                continue;
            }

            entry.TryGetValue("klasor", out var folder);
            entry.TryGetValue("kategori", out var categoryName);
            entry.TryGetValue("fiyat", out var priceText);
            entry.TryGetValue("kisa_aciklama", out var shortDescription);
            entry.TryGetValue("aciklama", out var description);

            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(categoryName))
            {
                logger.LogWarning("seed-images/urunler.txt: '{Name}' için 'klasor' ya da 'kategori' eksik, atlandı.", name);
                continue;
            }

            if (!decimal.TryParse(priceText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var price))
            {
                logger.LogWarning("seed-images/urunler.txt: '{Name}' için geçerli bir 'fiyat' yok, atlandı.", name);
                continue;
            }

            var category = await context.Categories.FirstOrDefaultAsync(c => c.Name == categoryName);
            if (category is null)
            {
                category = new Category
                {
                    Name = categoryName,
                    Slug = Slugify(categoryName),
                    IsActive = true,
                    DisplayOrder = await context.Categories.CountAsync() + 1
                };
                await context.Categories.AddAsync(category);
                await context.SaveChangesAsync();
            }

            var product = new Product
            {
                Name = name,
                Slug = await GenerateUniqueSlugAsync(context, name),
                ShortDescription = shortDescription,
                Description = description,
                Price = price,
                CategoryId = category.Id,
                Status = ProductStatus.Active,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow
            };

            var variants = ParseVariants(entry);
            if (variants.Count == 0)
            {
                logger.LogWarning(
                    "seed-images/urunler.txt: '{Name}' için 'bedenler' ya da 'stok' satırı yok — ürün stoksuz eklendi ve satın alınamayacak. " +
                    "Manifest'e stok ekleyip uygulamayı yeniden başlatın ya da admin panelinden varyant girin.", name);
            }
            foreach (var variant in variants)
                product.Variants.Add(variant);

            var photosDir = Path.Combine(seedRoot, folder);
            if (Directory.Exists(photosDir))
            {
                var allFiles = Directory.GetFiles(photosDir).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
                var files = allFiles.Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();

                // This is a server-only batch import (no browser in the loop, unlike the admin
                // upload form), so there's no HEIC→JPEG auto-conversion here — ImageSharp can't
                // decode HEIC at all. Flag it loudly instead of silently shipping an imageless
                // product, since that failure mode is easy to miss.
                var skippedHeic = allFiles.Except(files)
                    .Where(f => f.EndsWith(".heic", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".heif", StringComparison.OrdinalIgnoreCase))
                    .Select(Path.GetFileName)
                    .ToList();
                if (skippedHeic.Count > 0)
                {
                    logger.LogWarning(
                        "seed-images/{Folder}: {Count} HEIC/HEIF dosyası atlandı ({Files}) — bu klasörden içe aktarma tarayıcı dönüşümü içermiyor, " +
                        "bu dosyaları önce JPG/PNG'ye çevirip tekrar denemeniz gerekiyor ('{Name}' ürünü).",
                        folder, skippedHeic.Count, string.Join(", ", skippedHeic), name);
                }
                var otherSkipped = allFiles.Except(files).Select(Path.GetFileName).Except(skippedHeic).ToList();
                if (otherSkipped.Count > 0)
                {
                    logger.LogWarning("seed-images/{Folder}: desteklenmeyen formatta {Count} dosya atlandı ({Files}).", folder, otherSkipped.Count, string.Join(", ", otherSkipped));
                }
                if (files.Count == 0 && allFiles.Count > 0)
                {
                    logger.LogWarning("seed-images/{Folder}: klasörde hiç desteklenen görsel bulunamadı, '{Name}' görselsiz eklendi.", folder, name);
                }

                int order = 0;
                foreach (var file in files)
                {
                    var url = await fileService.ImportLocalImageAsync(file, "products");
                    product.Images.Add(new ProductImage
                    {
                        ImageUrl = url,
                        AltText = name,
                        IsMain = order == 0,
                        DisplayOrder = order++
                    });
                    if (order == 1) product.MainImageUrl = url;
                }
            }
            else
            {
                logger.LogWarning("seed-images/{Folder} klasörü bulunamadı ('{Name}' için görselsiz eklendi).", folder, name);
            }

            await context.Products.AddAsync(product);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>Manifest'teki stok satırlarını varyantlara çevirir.
    /// "bedenler: S=10, M=15, L=0" → beden başına bir varyant (0 stoklu beden de eklenir ki
    /// sitede "tükendi" olarak görünsün). Bedensiz ürünler (ör. çanta) için "stok: 8" →
    /// tek varyant. İkisi de yoksa boş liste döner.</summary>
    private static List<ProductVariant> ParseVariants(Dictionary<string, string> entry)
    {
        var variants = new List<ProductVariant>();

        if (entry.TryGetValue("bedenler", out var sizesText) && !string.IsNullOrWhiteSpace(sizesText))
        {
            foreach (var part in sizesText.Split(','))
            {
                var pieces = part.Split('=');
                var size = pieces[0].Trim().ToUpperInvariant();
                if (size.Length == 0) continue;

                var qty = 0;
                if (pieces.Length > 1) int.TryParse(pieces[1].Trim(), out qty);
                variants.Add(new ProductVariant { Size = size, StockQuantity = qty, IsActive = true });
            }
        }
        else if (entry.TryGetValue("stok", out var stockText) && int.TryParse(stockText, out var stock))
        {
            variants.Add(new ProductVariant { StockQuantity = stock, IsActive = true });
        }

        return variants;
    }

    private static List<Dictionary<string, string>> ParseManifest(string text)
    {
        var blocks = new List<Dictionary<string, string>>();
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                if (current.Count > 0) { blocks.Add(current); current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); }
                continue;
            }
            if (line.StartsWith('#')) continue;

            var colonIndex = line.IndexOf(':');
            if (colonIndex <= 0) continue;

            var key = line[..colonIndex].Trim().ToLowerInvariant();
            var value = line[(colonIndex + 1)..].Trim();
            if (value.Length > 0) current[key] = value;
        }
        if (current.Count > 0) blocks.Add(current);

        return blocks;
    }

    private static string Slugify(string text)
    {
        var slug = text.ToLowerInvariant()
            .Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u")
            .Replace("ş", "s").Replace("ö", "o").Replace("ç", "c")
            .Replace(" ", "-");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
        return System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-").Trim('-');
    }

    private static async Task<string> GenerateUniqueSlugAsync(AppDbContext context, string name)
    {
        var baseSlug = Slugify(name);
        var slug = baseSlug;
        var counter = 1;
        // IgnoreQueryFilters: the Product query filter hides soft-deleted rows, but the DB's
        // unique index on Slug still covers them — checking without it would hand out a slug
        // that's actually still taken and crash the INSERT with a 23505 duplicate key error.
        while (await context.Products.IgnoreQueryFilters().AnyAsync(p => p.Slug == slug))
            slug = $"{baseSlug}-{counter++}";
        return slug;
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles = ["Admin", "Customer"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private static async Task SeedAdminUserAsync(UserManager<AppUser> userManager)
    {
        const string adminEmail = "admin@kedwear.com";
        if (await userManager.FindByEmailAsync(adminEmail) is not null) return;

        var admin = new AppUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FirstName = "Admin",
            LastName = "KedWear",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, "Admin@123456");
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }

    /// <summary>Navigasyondaki "Koleksiyon" menüsü bu sabit kategorilere link verir (slug ile),
    /// o yüzden hepsinin DB'de var olması garanti edilir. Slug bazlı upsert: eksik olan eklenir,
    /// var olan olduğu gibi bırakılır (admin'in ad/sıra düzenlemeleri ezilmez) — tek istisna,
    /// hâlâ eski seed adını taşıyan kategorinin yeni ada taşınması ve yanlışlıkla silinen
    /// canonical kategorinin geri açılması. Her açılışta çalışır, idempotenttir.</summary>
    private static async Task SeedCategoriesAsync(AppDbContext context)
    {
        var canonical = new List<Category>
        {
            new() { Name = "Çanta", Slug = "canta", Description = "Kanvas ve el yapımı çantalar", DisplayOrder = 1, IsActive = true,
                ImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_7c6d5f72e7b76f9a63a5acf38217b238.jpeg" },
            new() { Name = "Rüzgarlık/Yağmurluk", Slug = "ruzgarlik-yagmurluk", Description = "Rüzgarlık ve yağmurluk modelleri", DisplayOrder = 2, IsActive = true },
            new() { Name = "Hoodie/Sweatshirt", Slug = "hoodie", Description = "Sweatshirt ve hoodie modelleri", DisplayOrder = 3, IsActive = true,
                ImageUrl = "https://images.unsplash.com/photo-1556821840-3a63f15732ce?w=600&q=80" },
            new() { Name = "Çorap/Şapka", Slug = "corap", Description = "Baskılı ve renkli çorap ve şapkalar", DisplayOrder = 4, IsActive = true },
            new() { Name = "T-Shirt", Slug = "tisort", Description = "Oversize ve baskılı tişörtler", DisplayOrder = 5, IsActive = true,
                ImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_14bba417f7cb6348319bb8dae4a545ba.jpeg" },
            new() { Name = "Eşofman Alt", Slug = "esofman-alt", Description = "Rahat kesim eşofman altları", DisplayOrder = 6, IsActive = true },
            new() { Name = "Pantolon", Slug = "pantolon", Description = "Jogger ve günlük pantolonlar", DisplayOrder = 7, IsActive = true,
                ImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_15536f67f94865229083841f07eb0152.jpeg" },
            new() { Name = "Alt Üst Takımlar", Slug = "takim", Description = "Alt üst takımlar ve setler", DisplayOrder = 8, IsActive = true,
                ImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_94d3e0c9143ea6a8b995dfdee3a671c5.jpeg" },
        };

        // Eski seed adları → canonical ad geçişi (yalnızca admin adı hiç değiştirmediyse uygulanır)
        var legacyNames = new Dictionary<string, string> { ["hoodie"] = "Hoodie", ["takim"] = "Takım", ["corap"] = "Çorap" };

        foreach (var cat in canonical)
        {
            var existing = await context.Categories.FirstOrDefaultAsync(c => c.Slug == cat.Slug);
            if (existing is null)
            {
                await context.Categories.AddAsync(cat);
                continue;
            }

            if (legacyNames.TryGetValue(cat.Slug, out var oldName) && existing.Name == oldName)
                existing.Name = cat.Name;

            if (existing.IsDeleted || !existing.IsActive)
            {
                existing.IsDeleted = false;
                existing.IsActive = true;
            }
        }

        await context.SaveChangesAsync();
    }
}
