using FethiyeCicekcisi.Core.Entities;
using FethiyeCicekcisi.Core.Enums;
using FethiyeCicekcisi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FethiyeCicekcisi.Infrastructure.Data.Seed;

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
            await SeedOccasionsAsync(context);
            await SeedDeliveryZonesAsync(context);
            await SeedDemoProductsAsync(context);
            await SeedRealProductsFromFolderAsync(scope.ServiceProvider, context, logger);
            await BackfillProductCodesAsync(context);
            logger.LogInformation("Seed data başarıyla yüklendi.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Seed data yüklenirken hata oluştu.");
        }
    }

    /// <summary>Reads src/FethiyeCicekcisi.Web/seed-images/urunler.txt (a simple hand-editable
    /// manifest) and, for each block, imports the product's photos from its sibling folder and
    /// creates a real Product + ProductImage rows — the "launch with my actual catalog already
    /// loaded" path, distinct from the Unsplash demo products above. Runs every startup and is
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
                // manifest after the product was first imported, so backfill variants once —
                // but never overwrite live stock counts, which the admin may have adjusted since.
                if (existing.Variants.Count == 0)
                {
                    var backfill = ParseVariants(entry);
                    if (backfill.Count > 0)
                    {
                        foreach (var v in backfill) existing.Variants.Add(v);
                        await context.SaveChangesAsync();
                        logger.LogInformation("seed-images/urunler.txt: '{Name}' için stok/seçenek bilgisi sonradan eklendi.", name);
                    }
                }
                continue;
            }

            entry.TryGetValue("klasor", out var folder);
            entry.TryGetValue("kategori", out var categoryName);
            entry.TryGetValue("fiyat", out var priceText);
            entry.TryGetValue("kisa_aciklama", out var shortDescription);
            entry.TryGetValue("aciklama", out var description);
            entry.TryGetValue("bakim", out var careInstructions);

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
                CareInstructions = careInstructions,
                Price = price,
                CategoryId = category.Id,
                Status = ProductStatus.Active,
                IsFeatured = true,
                ContainsVase = ParseBool(entry, "vazolu"),
                AllowsMessageCard = !entry.ContainsKey("kart_yok"),
                IsSameDayDelivery = ParseBool(entry, "ayni_gun"),
                CreatedAt = DateTime.UtcNow
            };

            // "ozel_gunler: Anneler Günü, Doğum Günü" → mevcut özel gün etiketleri eşlenir.
            if (entry.TryGetValue("ozel_gunler", out var occasionsText) && !string.IsNullOrWhiteSpace(occasionsText))
            {
                foreach (var occasionName in occasionsText.Split(',').Select(o => o.Trim()).Where(o => o.Length > 0))
                {
                    var occasion = await context.Occasions.FirstOrDefaultAsync(o => o.Name == occasionName);
                    if (occasion is not null)
                        product.ProductOccasions.Add(new ProductOccasion { Occasion = occasion });
                    else
                        logger.LogWarning("seed-images/urunler.txt: '{Name}' için '{Occasion}' özel günü bulunamadı, atlandı.", name, occasionName);
                }
            }

            var variants = ParseVariants(entry);
            if (variants.Count == 0)
            {
                logger.LogWarning(
                    "seed-images/urunler.txt: '{Name}' için 'secenekler' ya da 'stok' satırı yok — ürün stoksuz eklendi ve satın alınamayacak. " +
                    "Manifest'e stok ekleyip uygulamayı yeniden başlatın ya da admin panelinden seçenek girin.", name);
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

    private static bool ParseBool(Dictionary<string, string> entry, string key) =>
        entry.TryGetValue(key, out var value) &&
        (value.Equals("evet", StringComparison.OrdinalIgnoreCase) || value == "1" ||
         value.Equals("true", StringComparison.OrdinalIgnoreCase));

    /// <summary>Manifest'teki stok satırlarını varyantlara çevirir.
    /// "secenekler: 11 Adet=10, 21 Adet=5, Büyük Boy=0" → seçenek başına bir varyant
    /// (0 stoklu seçenek de eklenir ki sitede "tükendi" olarak görünsün). Tek seçenekli
    /// ürünler için "stok: 8" → tek varyant. İkisi de yoksa boş liste döner.</summary>
    private static List<ProductVariant> ParseVariants(Dictionary<string, string> entry)
    {
        var variants = new List<ProductVariant>();

        if (entry.TryGetValue("secenekler", out var optionsText) && !string.IsNullOrWhiteSpace(optionsText))
        {
            foreach (var part in optionsText.Split(','))
            {
                var pieces = part.Split('=');
                var option = pieces[0].Trim();
                if (option.Length == 0) continue;

                var qty = 0;
                if (pieces.Length > 1) int.TryParse(pieces[1].Trim(), out qty);
                variants.Add(new ProductVariant { Size = option, StockQuantity = qty, IsActive = true });
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
        const string adminEmail = "admin@yoncacicekcilik.com";
        if (await userManager.FindByEmailAsync(adminEmail) is not null) return;

        var admin = new AppUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FirstName = "Admin",
            LastName = "Yonca Çiçekçilik",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, "Admin@123456");
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }

    /// <summary>Navigasyondaki "Koleksiyon" menüsü bu sabit kategorilere link verir (slug ile),
    /// o yüzden hepsinin DB'de var olması garanti edilir. Slug bazlı upsert: eksik olan eklenir,
    /// var olan olduğu gibi bırakılır (admin'in ad/sıra düzenlemeleri ezilmez); yanlışlıkla
    /// silinen canonical kategori geri açılır. Her açılışta çalışır, idempotenttir.</summary>
    private static async Task SeedCategoriesAsync(AppDbContext context)
    {
        var canonical = new List<Category>
        {
            new() { Name = "Buketler", Slug = "buketler", Description = "El bağlaması gül, papatya ve mevsim buketleri", DisplayOrder = 1, IsActive = true,
                ImageUrl = "https://images.unsplash.com/photo-1561181286-d3fee7d55364?w=800&q=80" },
            new() { Name = "Orkideler", Slug = "orkideler", Description = "Tek ve çift dal seramik saksıda orkideler", DisplayOrder = 2, IsActive = true,
                ImageUrl = "https://images.unsplash.com/photo-1524598171347-abf62dfd6694?w=800&q=80" },
            new() { Name = "Saksı Çiçekleri", Slug = "saksi-cicekleri", Description = "Ev ve ofis için saksıda yaşayan çiçekler", DisplayOrder = 3, IsActive = true,
                ImageUrl = "https://images.unsplash.com/photo-1485955900006-10f4d324d411?w=800&q=80" },
            new() { Name = "Çelenkler", Slug = "celenkler", Description = "Açılış, düğün ve cenaze çelenkleri", DisplayOrder = 4, IsActive = true },
            new() { Name = "Teraryumlar", Slug = "teraryumlar", Description = "Cam fanusta minyatür bahçeler", DisplayOrder = 5, IsActive = true },
            new() { Name = "Aranjmanlar", Slug = "aranjmanlar", Description = "Vazoda ve kutuda özel tasarım aranjmanlar", DisplayOrder = 6, IsActive = true,
                ImageUrl = "https://images.unsplash.com/photo-1487530811176-3780de880c2d?w=800&q=80" },
            new() { Name = "Yapay Çiçekler", Slug = "yapay-cicekler", Description = "Solmayan, bakım istemeyen yapay tasarımlar", DisplayOrder = 7, IsActive = true },
        };

        foreach (var cat in canonical)
        {
            var existing = await context.Categories.FirstOrDefaultAsync(c => c.Slug == cat.Slug);
            if (existing is null)
            {
                await context.Categories.AddAsync(cat);
                continue;
            }

            if (existing.IsDeleted || !existing.IsActive)
            {
                existing.IsDeleted = false;
                existing.IsActive = true;
            }
        }

        await context.SaveChangesAsync();
    }

    /// <summary>Özel gün etiketleri — ProductAdmin formundaki çoklu seçim kutuları ve
    /// sitedeki "Özel Günler" menüsü bunlardan beslenir. Slug bazlı upsert, idempotent.</summary>
    private static async Task SeedOccasionsAsync(AppDbContext context)
    {
        var canonical = new List<Occasion>
        {
            new() { Name = "Anneler Günü", Slug = "anneler-gunu", DisplayOrder = 1, IsActive = true },
            new() { Name = "Sevgililer Günü", Slug = "sevgililer-gunu", DisplayOrder = 2, IsActive = true },
            new() { Name = "Doğum Günü", Slug = "dogum-gunu", DisplayOrder = 3, IsActive = true },
            new() { Name = "Geçmiş Olsun", Slug = "gecmis-olsun", DisplayOrder = 4, IsActive = true },
            new() { Name = "Yeni İş", Slug = "yeni-is", DisplayOrder = 5, IsActive = true },
            new() { Name = "Kadınlar Günü", Slug = "kadinlar-gunu", DisplayOrder = 6, IsActive = true },
            new() { Name = "Öğretmenler Günü", Slug = "ogretmenler-gunu", DisplayOrder = 7, IsActive = true },
        };

        foreach (var occ in canonical)
        {
            var existing = await context.Occasions.FirstOrDefaultAsync(o => o.Slug == occ.Slug);
            if (existing is null)
            {
                await context.Occasions.AddAsync(occ);
                continue;
            }

            if (existing.IsDeleted || !existing.IsActive)
            {
                existing.IsDeleted = false;
                existing.IsActive = true;
            }
        }

        await context.SaveChangesAsync();
    }

    /// <summary>Teslimat bölgeleri — yalnızca tablo boşken (ilk kurulum) yüklenir;
    /// sonrasında admin panelinden yönetilir, seed bir daha dokunmaz.</summary>
    private static async Task SeedDeliveryZonesAsync(AppDbContext context)
    {
        if (await context.DeliveryZones.AnyAsync()) return;

        (string Name, decimal Fee)[] zones =
        [
            ("Merkez", 150), ("Çalış", 150), ("Patlangıç", 150), ("Karaçulha", 250),
            ("Çamköy", 250), ("Çatalarık", 250), ("Esenköy", 250), ("Koca Çalış", 300),
            ("Aksazlar Koyu", 350), ("Çiftlik", 350), ("Yanıklar", 350), ("Hisarönü", 400),
            ("Ovacık", 400), ("Kayaköy", 600), ("Ölüdeniz", 700), ("Kıdrak Koyu", 700),
            ("Yeşil Üzümlü", 750), ("Hillside", 800), ("Göcek", 900)
        ];

        var order = 1;
        foreach (var (name, fee) in zones)
            await context.DeliveryZones.AddAsync(new DeliveryZone
            {
                City = "Fethiye",
                Name = name,
                Fee = fee,
                DisplayOrder = order++,
                IsActive = true
            });

        await context.SaveChangesAsync();
    }

    /// <summary>Kodu boş kalan her ürüne YC-{Id:D4} formatında ürün kodu atar —
    /// "her ürünün bir kodu olsun" kuralını seed/manifest/admin farkı olmadan garanti eder.</summary>
    private static async Task BackfillProductCodesAsync(AppDbContext context)
    {
        var withoutCode = await context.Products.IgnoreQueryFilters()
            .Where(p => p.Code == null || p.Code == "").ToListAsync();
        if (withoutCode.Count == 0) return;
        foreach (var p in withoutCode)
            p.Code = $"YC-{p.Id:D4}";
        await context.SaveChangesAsync();
    }

    /// <summary>Vitrin boş açılmasın diye Unsplash görselli örnek çiçek ürünleri — yalnızca
    /// hiç ürün yokken (ilk kurulum) çalışır; admin kendi katalogunu girdikten sonra bu
    /// listeye bir daha dokunulmaz.</summary>
    private static async Task SeedDemoProductsAsync(AppDbContext context)
    {
        if (await context.Products.IgnoreQueryFilters().AnyAsync()) return;

        var categories = await context.Categories.ToDictionaryAsync(c => c.Slug);
        var occasions = await context.Occasions.ToDictionaryAsync(o => o.Slug);

        Product Make(string name, string categorySlug, decimal price, string shortDesc, string desc,
            string care, string imageUrl, bool vase, bool card, bool sameDay,
            (string Option, int Stock, decimal? Diff)[] variants, string[] occasionSlugs,
            decimal? discountPrice = null)
        {
            var p = new Product
            {
                Name = name,
                Slug = Slugify(name),
                ShortDescription = shortDesc,
                Description = desc,
                CareInstructions = care,
                Price = price,
                DiscountPrice = discountPrice,
                MainImageUrl = imageUrl,
                CategoryId = categories[categorySlug].Id,
                Status = ProductStatus.Active,
                IsFeatured = true,
                ContainsVase = vase,
                AllowsMessageCard = card,
                IsSameDayDelivery = sameDay,
                CreatedAt = DateTime.UtcNow
            };
            p.Images.Add(new ProductImage { ImageUrl = imageUrl, AltText = name, IsMain = true, DisplayOrder = 0 });
            foreach (var (option, stock, diff) in variants)
                p.Variants.Add(new ProductVariant { Size = option, StockQuantity = stock, PriceDifference = diff, IsActive = true });
            foreach (var slug in occasionSlugs)
                if (occasions.TryGetValue(slug, out var occ))
                    p.ProductOccasions.Add(new ProductOccasion { Occasion = occ });
            return p;
        }

        var demo = new List<Product>
        {
            Make("Kırmızı Gül Buketi", "buketler", 850m,
                "El bağlaması kırmızı gül buketi, kraft ambalajda",
                "İthal kırmızı güllerden el bağlaması buket. Kraft kağıt ve saten kurdele ile paketlenir.",
                "Sapları çapraz kesip vazodaki suyu her gün tazeleyin; doğrudan güneş almayan serin bir yerde tutun.",
                "https://images.unsplash.com/photo-1561181286-d3fee7d55364?w=800&q=80",
                false, true, true,
                new[] { ("11 Adet", 15, (decimal?)null), ("21 Adet", 10, (decimal?)350m), ("41 Adet", 5, (decimal?)1100m) },
                new[] { "sevgililer-gunu", "dogum-gunu", "kadinlar-gunu" }),

            Make("Pembe Mevsim Buketi", "buketler", 650m,
                "Mevsim çiçeklerinden pembe tonlarda buket",
                "Mevsimin en taze çiçeklerinden pembe-pudra tonlarında hazırlanan tasarım buket.",
                "Vazoya koymadan önce sapları eğik kesin, iki günde bir suyunu değiştirin.",
                "https://images.unsplash.com/photo-1490750967868-88aa4486c946?w=800&q=80",
                false, true, true,
                new[] { ("Standart", 12, (decimal?)null), ("Büyük Boy", 6, (decimal?)250m) },
                new[] { "dogum-gunu", "anneler-gunu", "gecmis-olsun" },
                discountPrice: 550m),

            Make("Çift Dal Beyaz Orkide", "orkideler", 1250m,
                "Seramik saksıda çift dal beyaz phalaenopsis orkide",
                "Zarif seramik saksıda, uzun ömürlü çift dal beyaz phalaenopsis orkide.",
                "Haftada bir kez köke 1 su bardağı su verin; yapraklara su değdirmeyin, bol dolaylı ışıkta tutun.",
                "https://images.unsplash.com/photo-1524598171347-abf62dfd6694?w=800&q=80",
                true, true, true,
                new[] { ("Çift Dal", 8, (decimal?)null), ("Tek Dal", 10, (decimal?)-400m) },
                new[] { "yeni-is", "anneler-gunu", "ogretmenler-gunu" }),

            Make("Kutuda Renkli Aranjman", "aranjmanlar", 950m,
                "Özel tasarım kutuda mevsim çiçekleri aranjmanı",
                "Şık silindir kutuda gül, lisyantus ve mevsim yeşillikleriyle hazırlanan aranjman. Kutusuyla teslim edilir, vazo gerektirmez.",
                "Kutudaki süngere iki günde bir az miktarda su ekleyin.",
                "https://images.unsplash.com/photo-1487530811176-3780de880c2d?w=800&q=80",
                true, true, true,
                new[] { ("Standart", 10, (decimal?)null), ("Büyük Boy", 4, (decimal?)300m) },
                new[] { "dogum-gunu", "kadinlar-gunu", "yeni-is" }),

            Make("Sukulent Teraryum", "teraryumlar", 480m,
                "Cam fanusta sukulent ve kaktüs bahçesi",
                "Cam fanus içinde sukulent, kaktüs ve doğal taşlarla hazırlanan minyatür bahçe.",
                "Ayda bir-iki kez köklere az miktarda su verin; aydınlık ama doğrudan güneş görmeyen yerde tutun.",
                "https://images.unsplash.com/photo-1485955900006-10f4d324d411?w=800&q=80",
                true, true, false,
                new[] { ("Küçük Boy", 12, (decimal?)null), ("Büyük Boy", 6, (decimal?)220m) },
                new[] { "yeni-is", "ogretmenler-gunu" }),

            Make("Açılış Çelengi", "celenkler", 1750m,
                "Açılış ve kutlamalar için ayaklı çelenk",
                "İşyeri açılışları ve kutlamalar için gerbera ve glayöllerle hazırlanan ayaklı çelenk. Kurdele yazısı sipariş notuna eklenebilir.",
                "Teslimattan sonra bakım gerektirmez.",
                "https://images.unsplash.com/photo-1519378058457-4c29a0a2efac?w=800&q=80",
                false, false, true,
                new[] { ("Tek Katlı", 5, (decimal?)null), ("Çift Katlı", 3, (decimal?)650m) },
                new[] { "yeni-is" }),

            Make("Saksıda Barış Çiçeği (Spathiphyllum)", "saksi-cicekleri", 520m,
                "Hava temizleyen, gölgeye dayanıklı barış çiçeği",
                "Ev ve ofisler için bakımı kolay, beyaz çiçekli spathiphyllum. Dekoratif saksısıyla gönderilir.",
                "Toprağın üstü kuruyunca sulayın; loş ortamlara dayanıklıdır.",
                "https://images.unsplash.com/photo-1470058869958-2a77ade41c02?w=800&q=80",
                true, true, false,
                new[] { ("Standart", 9, (decimal?)null) },
                new[] { "gecmis-olsun", "yeni-is" }),

            Make("Yapay Şakayık Aranjmanı", "yapay-cicekler", 720m,
                "Solmayan yapay şakayıklarla cam vazoda aranjman",
                "Gerçeğinden ayırt edilmesi güç yapay şakayıklarla hazırlanan, yıllarca solmayan aranjman.",
                "Ara sıra kuru bezle tozunu almanız yeterli.",
                "https://images.unsplash.com/photo-1520763185298-1b434c919102?w=800&q=80",
                true, true, false,
                new[] { ("Standart", 7, (decimal?)null) },
                new[] { "dogum-gunu", "anneler-gunu" }),
        };

        await context.Products.AddRangeAsync(demo);
        await context.SaveChangesAsync();
    }
}
