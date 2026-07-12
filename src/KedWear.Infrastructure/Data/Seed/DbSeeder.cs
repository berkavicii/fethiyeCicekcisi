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
            await SeedProductsAsync(context);
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

            if (await context.Products.AnyAsync(p => p.Name == name))
                continue; // already seeded on a previous run

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
        while (await context.Products.AnyAsync(p => p.Slug == slug))
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

    private static async Task SeedCategoriesAsync(AppDbContext context)
    {
        if (await context.Categories.AnyAsync()) return;

        var categories = new List<Category>
        {
            new() { Name = "T-Shirt", Slug = "tisort", Description = "Oversize ve baskılı tişörtler", DisplayOrder = 1, IsActive = true,
                ImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_14bba417f7cb6348319bb8dae4a545ba.jpeg" },
            new() { Name = "Çanta", Slug = "canta", Description = "Kanvas ve el yapımı çantalar", DisplayOrder = 2, IsActive = true,
                ImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_7c6d5f72e7b76f9a63a5acf38217b238.jpeg" },
            new() { Name = "Pantolon", Slug = "pantolon", Description = "Jogger ve günlük pantolonlar", DisplayOrder = 3, IsActive = true,
                ImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_15536f67f94865229083841f07eb0152.jpeg" },
            new() { Name = "Takım", Slug = "takim", Description = "Alt üst takımlar ve setler", DisplayOrder = 4, IsActive = true,
                ImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_94d3e0c9143ea6a8b995dfdee3a671c5.jpeg" },
            new() { Name = "Hoodie", Slug = "hoodie", Description = "Sweatshirt ve hoodie modelleri", DisplayOrder = 5, IsActive = true,
                ImageUrl = "https://images.unsplash.com/photo-1556821840-3a63f15732ce?w=600&q=80" },
        };

        await context.Categories.AddRangeAsync(categories);
        await context.SaveChangesAsync();
    }

    private static async Task SeedProductsAsync(AppDbContext context)
    {
        if (await context.Products.AnyAsync()) return;

        var tisort   = await context.Categories.FirstAsync(c => c.Slug == "tisort");
        var canta    = await context.Categories.FirstAsync(c => c.Slug == "canta");
        var pantolon = await context.Categories.FirstAsync(c => c.Slug == "pantolon");
        var takim    = await context.Categories.FirstAsync(c => c.Slug == "takim");
        var hoodie   = await context.Categories.FirstAsync(c => c.Slug == "hoodie");

        var products = new List<Product>
        {
            new()
            {
                Name = "TERMINAL AA1 OVERSIZE TİŞÖRT",
                Slug = "terminal-aa1-oversize-tisort",
                ShortDescription = "Terminal Departures Ön ve Arka Baskılı Oversize Tişört",
                Description = "Terminal Departures ön ve arka baskılı oversize tişört. Her parça bir yolculuk hikayesi anlatır. 30 derecede ters çevirerek yıkayınız.",
                Price = 750.00m,
                CategoryId = tisort.Id,
                IsFeatured = true,
                Status = ProductStatus.Active,
                Material = "%100 Pamuk Oversize",
                CareInstructions = "30°C ters çevirerek yıkayın",
                MainImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_14bba417f7cb6348319bb8dae4a545ba.jpeg",
                Variants = new List<ProductVariant>
                {
                    new() { Size = "S", StockQuantity = 10, IsActive = true },
                    new() { Size = "M", StockQuantity = 15, IsActive = true },
                    new() { Size = "L", StockQuantity = 12, IsActive = true },
                    new() { Size = "XL", StockQuantity = 8, IsActive = true },
                }
            },
            new()
            {
                Name = "PASIFIC REPUBLIC OVERSIZE TİŞÖRT",
                Slug = "pasific-republic-oversize-tisort",
                ShortDescription = "Pasific Republic Ön ve Arka Yazı Baskılı Oversize Tişört",
                Description = "Pasific Republic ön ve arka yazı baskılı oversize tişört. 30 derecede ters çevirerek yıkayınız. S M L XL beden ölçüleri mevcuttur.",
                Price = 750.00m,
                CategoryId = tisort.Id,
                IsFeatured = true,
                Status = ProductStatus.Active,
                Material = "%100 Pamuk Oversize",
                CareInstructions = "30°C ters çevirerek yıkayın",
                MainImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_09df88659695edfd8e44fb389a9d2a73.jpeg",
                Variants = new List<ProductVariant>
                {
                    new() { Size = "S", StockQuantity = 8, IsActive = true },
                    new() { Size = "M", StockQuantity = 12, IsActive = true },
                    new() { Size = "L", StockQuantity = 10, IsActive = true },
                    new() { Size = "XL", StockQuantity = 6, IsActive = true },
                }
            },
            new()
            {
                Name = "SOLEIL ÖN BASKILI OVERSIZE TİŞÖRT",
                Slug = "soleil-on-baskili-oversize-tisort",
                ShortDescription = "Soleil Ön Baskılı Oversize Tişört",
                Description = "Soleil ön baskılı oversize tişört. 30 derecede ters çevirip yıkayınız. S M L XL beden ölçüleri mevcuttur.",
                Price = 750.00m,
                CategoryId = tisort.Id,
                IsFeatured = true,
                Status = ProductStatus.Active,
                Material = "%100 Pamuk Oversize",
                CareInstructions = "30°C ters çevirerek yıkayın",
                MainImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_0c3e16cf2e7927c702685550acb418ca.jpeg",
                Variants = new List<ProductVariant>
                {
                    new() { Size = "S", StockQuantity = 10, IsActive = true },
                    new() { Size = "M", StockQuantity = 14, IsActive = true },
                    new() { Size = "L", StockQuantity = 10, IsActive = true },
                    new() { Size = "XL", StockQuantity = 5, IsActive = true },
                }
            },
            new()
            {
                Name = "NEVER SURF ALONE OVERSIZE TİŞÖRT",
                Slug = "never-surf-alone-oversize-tisort",
                ShortDescription = "Never Surf Alone Ön ve Arka Baskılı Oversize Tişört",
                Description = "Never Surf Alone ön ve arka baskılı oversize tişört. 30 derecede ters çevirip yıkayınız. S M L XL beden ölçüleri mevcuttur.",
                Price = 750.00m,
                CategoryId = tisort.Id,
                IsFeatured = false,
                Status = ProductStatus.Active,
                Material = "%100 Pamuk Oversize",
                CareInstructions = "30°C ters çevirerek yıkayın",
                MainImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_0cf9e8a911ee202b849169a144ffbe68.jpeg",
                Variants = new List<ProductVariant>
                {
                    new() { Size = "S", StockQuantity = 7, IsActive = true },
                    new() { Size = "M", StockQuantity = 11, IsActive = true },
                    new() { Size = "L", StockQuantity = 9, IsActive = true },
                    new() { Size = "XL", StockQuantity = 4, IsActive = true },
                }
            },
            new()
            {
                Name = "EL CAMINO BASKILI TİŞÖRT",
                Slug = "el-camino-baskili-tisort",
                ShortDescription = "El Camino Ön ve Arka Baskılı Tişört",
                Description = "El Camino ön ve arka baskılı tişört. M L XL beden ölçüleri mevcuttur. Manken ölçüleri: 173 boy 72 kg L beden. 30 derecede ters çevirerek yıkayınız.",
                Price = 600.00m,
                CategoryId = tisort.Id,
                IsFeatured = false,
                Status = ProductStatus.Active,
                Material = "%100 Pamuk",
                CareInstructions = "30°C ters çevirerek yıkayın",
                MainImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_baaf1614324222d924e1bdc6500ba65e.jpeg",
                Variants = new List<ProductVariant>
                {
                    new() { Size = "M", StockQuantity = 12, IsActive = true },
                    new() { Size = "L", StockQuantity = 10, IsActive = true },
                    new() { Size = "XL", StockQuantity = 7, IsActive = true },
                }
            },
            new()
            {
                Name = "JOURNEY BASKILI TİŞÖRT",
                Slug = "journey-baskili-tisort",
                ShortDescription = "Journey Ön ve Arka Baskılı Tişört",
                Description = "Journey ön ve arka baskılı tişört. M L XL beden ölçüleri mevcuttur. Manken ölçüleri: 173 boy 72 kg L beden. 30 derecede ters çevirerek yıkayınız.",
                Price = 600.00m,
                CategoryId = tisort.Id,
                IsFeatured = false,
                Status = ProductStatus.Active,
                Material = "%100 Pamuk",
                CareInstructions = "30°C ters çevirerek yıkayın",
                MainImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_e0b4bd99b62a320b1626cb372080bd0d.jpeg",
                Variants = new List<ProductVariant>
                {
                    new() { Size = "M", StockQuantity = 10, IsActive = true },
                    new() { Size = "L", StockQuantity = 12, IsActive = true },
                    new() { Size = "XL", StockQuantity = 6, IsActive = true },
                }
            },
            new()
            {
                Name = "HARDAL STAY SALTY KANVAS ÇANTA",
                Slug = "hardal-stay-salty-kanvas-canta",
                ShortDescription = "Hardal rengi Stay Salty kanvas çanta",
                Description = "Kum rengi kanvas çanta. Çanta ölçüleri: En 37 cm, Boy 37 cm. Dayanıklı kanvas kumaş, Stay Salty baskı.",
                Price = 1450.00m,
                CategoryId = canta.Id,
                IsFeatured = true,
                Status = ProductStatus.Active,
                Material = "Kanvas",
                CareInstructions = "Nemli bez ile silin",
                MainImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_7c6d5f72e7b76f9a63a5acf38217b238.jpeg",
                Variants = new List<ProductVariant>
                {
                    new() { Color = "Hardal", ColorCode = "#C8A84B", StockQuantity = 8, IsActive = true },
                }
            },
            new()
            {
                Name = "KAHVE STAY SALTY KANVAS ÇANTA",
                Slug = "kahve-stay-salty-kanvas-canta",
                ShortDescription = "Kahve rengi Stay Salty kanvas çanta",
                Description = "Kahve rengi kanvas çanta. Çanta ölçüleri: En 37 cm, Boy 37 cm. Dayanıklı kanvas kumaş, Stay Salty baskı.",
                Price = 1450.00m,
                CategoryId = canta.Id,
                IsFeatured = true,
                Status = ProductStatus.Active,
                Material = "Kanvas",
                CareInstructions = "Nemli bez ile silin",
                MainImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_f335e287f0e065f5483cdb514e9d16c6.jpeg",
                Variants = new List<ProductVariant>
                {
                    new() { Color = "Kahve", ColorCode = "#6B4226", StockQuantity = 6, IsActive = true },
                }
            },
            new()
            {
                Name = "JOGGER PANTOLON x PARMESAN CHEESE KOMBİN",
                Slug = "jogger-pantolon-parmesan-cheese-kombin",
                ShortDescription = "Jogger pantolon ve Parmesan Cheese kombini",
                Description = "Jogger Pantolon x Parmesan Cheese Kombin. Beden belirtiniz. Rahat ve şık günlük kullanım için ideal jogger pantolon kombini.",
                Price = 1500.00m,
                CategoryId = takim.Id,
                IsFeatured = true,
                Status = ProductStatus.Active,
                Material = "Karışık Kumaş",
                CareInstructions = "30°C'de yıkayın",
                MainImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_15536f67f94865229083841f07eb0152.jpeg",
                Variants = new List<ProductVariant>
                {
                    new() { Size = "S", StockQuantity = 5, IsActive = true },
                    new() { Size = "M", StockQuantity = 8, IsActive = true },
                    new() { Size = "L", StockQuantity = 6, IsActive = true },
                    new() { Size = "XL", StockQuantity = 4, IsActive = true },
                }
            },
            new()
            {
                Name = "SUMMER 3'LÜ SET",
                Slug = "summer-3lu-set",
                ShortDescription = "Yaz sezonu için özel 3'lü set",
                Description = "30 derecede ters çevirip yıkayınız. Tişört modellerimiz Unisex'tir. S M L XL beden ölçüleri mevcuttur.",
                Price = 1800.00m,
                CategoryId = takim.Id,
                IsFeatured = true,
                Status = ProductStatus.Active,
                Material = "%100 Pamuk",
                CareInstructions = "30°C ters çevirerek yıkayın",
                MainImageUrl = "https://cdn.shopier.app/pictures_large/kedwear_94d3e0c9143ea6a8b995dfdee3a671c5.jpeg",
                Variants = new List<ProductVariant>
                {
                    new() { Size = "S", StockQuantity = 5, IsActive = true },
                    new() { Size = "M", StockQuantity = 7, IsActive = true },
                    new() { Size = "L", StockQuantity = 5, IsActive = true },
                    new() { Size = "XL", StockQuantity = 3, IsActive = true },
                }
            },
        };

        await context.Products.AddRangeAsync(products);
        await context.SaveChangesAsync();
    }
}
