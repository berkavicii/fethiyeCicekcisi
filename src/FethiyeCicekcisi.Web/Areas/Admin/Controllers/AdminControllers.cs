using FethiyeCicekcisi.Application.Services;
using FethiyeCicekcisi.Core.Entities;
using FethiyeCicekcisi.Core.Enums;
using FethiyeCicekcisi.Core.Interfaces.Services;
using FethiyeCicekcisi.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FethiyeCicekcisi.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
[Route("admin")]
public class DashboardController : Controller
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;
    private readonly OrderService _orderService;

    public DashboardController(ProductService productService, CategoryService categoryService, OrderService orderService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _orderService = orderService;
    }

    [HttpGet("")]
    [HttpGet("anasayfa")]
    public async Task<IActionResult> Index()
    {
        var (products, productCount, _) = await _productService.GetPagedProductsAsync(1, 5);
        var (orders, orderCount, _) = await _orderService.GetPagedOrdersAsync(1, 5);
        ViewBag.RecentProducts = products;
        ViewBag.RecentOrders = orders;
        ViewBag.ProductCount = productCount;
        ViewBag.OrderCount = orderCount;
        return View();
    }

    [HttpGet("istatistikler")]
    public async Task<IActionResult> Stats(string? donem = null)
    {
        DateTime? from = donem switch
        {
            "30" => DateTime.UtcNow.AddDays(-30),
            "90" => DateTime.UtcNow.AddDays(-90),
            "365" => DateTime.UtcNow.AddDays(-365),
            _ => null
        };

        var stats = await _orderService.GetProductSalesStatsAsync(from);
        return View(new AdminStatsViewModel { Stats = stats, Period = from is null ? null : donem });
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin")]
[Route("admin/urunler")]
public class ProductAdminController : Controller
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;
    private readonly OccasionService _occasionService;
    private readonly IFileService _fileService;
    private readonly ISlugService _slugService;

    public ProductAdminController(
        ProductService productService,
        CategoryService categoryService,
        OccasionService occasionService,
        IFileService fileService,
        ISlugService slugService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _occasionService = occasionService;
        _fileService = fileService;
        _slugService = slugService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, string? ara = null, int? kategori = null)
    {
        var (products, total, pages) = await _productService.GetPagedProductsAsync(page, 20, kategori, ara);
        var categories = await _categoryService.GetActiveCategoriesAsync();
        ViewBag.Products = products;
        ViewBag.TotalCount = total;
        ViewBag.TotalPages = pages;
        ViewBag.CurrentPage = page;
        ViewBag.Categories = categories;
        ViewBag.SearchTerm = ara;
        ViewBag.CategoryId = kategori;
        return View();
    }

    [HttpGet("ekle")]
    public async Task<IActionResult> Create()
    {
        return View(new AdminProductViewModel
        {
            Categories = await _categoryService.GetActiveCategoriesAsync(),
            Occasions = await _occasionService.GetActiveOccasionsAsync()
        });
    }

    [HttpPost("ekle")]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)] // 100MB total — several 12MB phone photos at once
    [RequestSizeLimit(104_857_600)]
    public async Task<IActionResult> Create(AdminProductViewModel model, List<IFormFile>? images)
    {
        if (!ModelState.IsValid)
        {
            model.Categories = await _categoryService.GetActiveCategoriesAsync();
            model.Occasions = await _occasionService.GetActiveOccasionsAsync();
            return View(model);
        }

        var slug = await _slugService.GenerateUniqueSlugAsync(model.Name, "product");

        var product = new Product
        {
            Name = model.Name,
            Code = model.Code,
            Slug = slug,
            Description = model.Description,
            ShortDescription = model.ShortDescription,
            Price = model.Price,
            DiscountPrice = model.DiscountPrice,
            CategoryId = model.CategoryId,
            IsFeatured = model.IsFeatured,
            Status = model.Status,
            CareInstructions = model.CareInstructions,
            ContainsVase = model.ContainsVase,
            AllowsMessageCard = model.AllowsMessageCard,
            IsSameDayDelivery = model.IsSameDayDelivery,
            DisplayOrder = model.DisplayOrder
        };

        // Add variants
        foreach (var v in model.Variants.Where(v => !v.IsDeleted))
        {
            product.Variants.Add(new ProductVariant
            {
                Size = v.Size,
                StockQuantity = v.StockQuantity,
                PriceDifference = v.PriceDifference,
                SKU = v.SKU,
                IsActive = v.IsActive
            });
        }

        // Occasion tags
        foreach (var occasionId in model.SelectedOccasionIds.Distinct())
            product.ProductOccasions.Add(new ProductOccasion { OccasionId = occasionId });

        await _productService.CreateProductAsync(product);

        // Upload images
        if (images?.Any() == true)
        {
            bool first = true;
            int order = 0;
            bool anyUploaded = false;
            var skipped = new List<string>();
            foreach (var img in images)
            {
                var validationError = _fileService.GetImageValidationError(img.FileName, img.Length);
                if (validationError is not null) { skipped.Add($"{img.FileName}: {validationError}"); continue; }
                await using var stream = img.OpenReadStream();
                if (!_fileService.IsValidImageContent(stream)) { skipped.Add($"{img.FileName}: {DescribeContentValidationFailure(stream)}"); continue; }
                var url = await _fileService.UploadImageAsync(stream, img.FileName);

                var productImage = new Core.Entities.ProductImage
                {
                    ProductId = product.Id,
                    ImageUrl = url,
                    AltText = product.Name,
                    IsMain = first,
                    DisplayOrder = order++
                };
                product.Images.Add(productImage);
                anyUploaded = true;

                if (first)
                    product.MainImageUrl = url;
                first = false;
            }

            // Persisted once after the whole batch — saving inside the loop would only
            // ever commit the first image and silently drop the rest.
            if (anyUploaded)
                await _productService.UpdateProductAsync(product);

            if (skipped.Any())
                TempData["Warning"] = BuildSkippedImagesMessage(skipped);
        }

        TempData["Success"] = "Ürün başarıyla eklendi.";
        return RedirectToAction("Index");
    }

    private static string BuildSkippedImagesMessage(List<string> skipped) =>
        $"{skipped.Count} görsel yüklenemedi — {string.Join(" • ", skipped)}. " +
        "Desteklenen formatlar: JPG, PNG, WEBP, GIF (HEIC/HEIF tarayıcıda otomatik JPEG'e çevrilir; çevrim başarısız olursa dosyayı manuel JPG'ye dönüştürüp tekrar yükleyin).";

    /// <summary>Distinguishes "this is a HEIC file the browser failed to convert before
    /// upload" (extension may say .jpg — some iOS share paths mislabel it) from a genuinely
    /// corrupt/unsupported file, so the admin gets an actionable reason either way.</summary>
    private string DescribeContentValidationFailure(Stream stream)
    {
        if (_fileService.IsHeicContent(stream))
            return "bu dosya hâlâ HEIC formatında (tarayıcı JPEG'e çeviremedi) — lütfen dosyayı manuel olarak JPG'ye dönüştürüp tekrar yükleyin";
        return "dosya içeriği bozuk ya da gerçek bir görsel değil";
    }

    [HttpGet("duzenle/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _productService.GetProductForAdminEditAsync(id);
        if (product is null) return NotFound();

        var vm = new AdminProductViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Code = product.Code,
            Description = product.Description,
            ShortDescription = product.ShortDescription,
            Price = product.Price,
            DiscountPrice = product.DiscountPrice,
            CategoryId = product.CategoryId,
            IsFeatured = product.IsFeatured,
            Status = product.Status,
            CareInstructions = product.CareInstructions,
            ContainsVase = product.ContainsVase,
            AllowsMessageCard = product.AllowsMessageCard,
            IsSameDayDelivery = product.IsSameDayDelivery,
            DisplayOrder = product.DisplayOrder,
            SelectedOccasionIds = product.ProductOccasions.Select(po => po.OccasionId).ToList(),
            Categories = await _categoryService.GetActiveCategoriesAsync(),
            Occasions = await _occasionService.GetActiveOccasionsAsync(),
            ExistingImages = product.Images.ToList(),
            Variants = product.Variants.Select(v => new AdminVariantViewModel
            {
                Id = v.Id,
                Size = v.Size,
                StockQuantity = v.StockQuantity,
                PriceDifference = v.PriceDifference,
                SKU = v.SKU,
                IsActive = v.IsActive
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost("duzenle/{id:int}")]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)]
    [RequestSizeLimit(104_857_600)]
    public async Task<IActionResult> Edit(int id, AdminProductViewModel model, List<IFormFile>? newImages)
    {
        if (!ModelState.IsValid)
        {
            model.Categories = await _categoryService.GetActiveCategoriesAsync();
            model.Occasions = await _occasionService.GetActiveOccasionsAsync();
            var p = await _productService.GetProductForAdminEditAsync(id);
            model.ExistingImages = p?.Images.ToList();
            return View(model);
        }

        var product = await _productService.GetProductForAdminEditAsync(id);
        if (product is null) return NotFound();

        product.Name = model.Name;
        product.Code = string.IsNullOrWhiteSpace(model.Code) ? product.Code : model.Code.Trim();
        product.Description = model.Description;
        product.ShortDescription = model.ShortDescription;
        product.Price = model.Price;
        product.DiscountPrice = model.DiscountPrice;
        product.CategoryId = model.CategoryId;
        product.IsFeatured = model.IsFeatured;
        product.Status = model.Status;
        product.CareInstructions = model.CareInstructions;
        product.ContainsVase = model.ContainsVase;
        product.AllowsMessageCard = model.AllowsMessageCard;
        product.IsSameDayDelivery = model.IsSameDayDelivery;
        product.DisplayOrder = model.DisplayOrder;

        // Özel gün etiketlerini formdaki seçimle eşitle (bileşik anahtarlı join tablosu).
        var selected = model.SelectedOccasionIds.Distinct().ToHashSet();
        foreach (var po in product.ProductOccasions.Where(po => !selected.Contains(po.OccasionId)).ToList())
            product.ProductOccasions.Remove(po);
        var current = product.ProductOccasions.Select(po => po.OccasionId).ToHashSet();
        foreach (var occasionId in selected.Where(oid => !current.Contains(oid)))
            product.ProductOccasions.Add(new ProductOccasion { ProductId = product.Id, OccasionId = occasionId });

        foreach (var v in model.Variants)
        {
            if (v.Id == 0)
            {
                if (v.IsDeleted) continue; // added then removed again before submit — never existed
                product.Variants.Add(new ProductVariant
                {
                    ProductId = product.Id,
                    Size = v.Size,
                    StockQuantity = v.StockQuantity,
                    PriceDifference = v.PriceDifference,
                    SKU = v.SKU,
                    IsActive = v.IsActive
                });
                continue;
            }

            var existing = product.Variants.FirstOrDefault(x => x.Id == v.Id);
            if (existing is null) continue;

            if (v.IsDeleted)
            {
                existing.IsDeleted = true;
                continue;
            }

            existing.Size = v.Size;
            existing.StockQuantity = v.StockQuantity;
            existing.PriceDifference = v.PriceDifference;
            existing.SKU = v.SKU;
            existing.IsActive = v.IsActive;
        }

        await _productService.UpdateProductAsync(product);

        if (newImages?.Any() == true)
        {
            bool hasMain = product.Images.Any(i => i.IsMain);
            int order = product.Images.Any() ? product.Images.Max(i => i.DisplayOrder) + 1 : 0;
            var skipped = new List<string>();
            foreach (var img in newImages)
            {
                var validationError = _fileService.GetImageValidationError(img.FileName, img.Length);
                if (validationError is not null) { skipped.Add($"{img.FileName}: {validationError}"); continue; }
                await using var stream = img.OpenReadStream();
                if (!_fileService.IsValidImageContent(stream)) { skipped.Add($"{img.FileName}: {DescribeContentValidationFailure(stream)}"); continue; }
                var url = await _fileService.UploadImageAsync(stream, img.FileName);

                var productImage = new Core.Entities.ProductImage
                {
                    ProductId = product.Id,
                    ImageUrl = url,
                    AltText = product.Name,
                    IsMain = !hasMain,
                    DisplayOrder = order++
                };
                product.Images.Add(productImage);

                if (!hasMain)
                {
                    product.MainImageUrl = url;
                    hasMain = true;
                }
            }
            await _productService.UpdateProductAsync(product);

            if (skipped.Any())
                TempData["Warning"] = BuildSkippedImagesMessage(skipped);
        }

        TempData["Success"] = "Ürün güncellendi.";
        return RedirectToAction("Edit", new { id });
    }

    [HttpPost("gorsel-sil/{imageId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int imageId, int productId)
    {
        await _productService.DeleteProductImageAsync(imageId);
        TempData["Success"] = "Görsel silindi.";
        return RedirectToAction("Edit", new { id = productId });
    }

    [HttpPost("gorsel-ana-yap/{imageId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMainImage(int imageId, int productId)
    {
        await _productService.SetMainProductImageAsync(productId, imageId);
        TempData["Success"] = "Ana görsel güncellendi.";
        return RedirectToAction("Edit", new { id = productId });
    }

    [HttpPost("sil/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _productService.DeleteProductAsync(id);
        switch (result)
        {
            case ProductDeleteResult.HardDeleted:
                TempData["Success"] = "Ürün tamamen silindi.";
                break;
            case ProductDeleteResult.SoftDeleted:
                TempData["Success"] = "Ürün geçmiş siparişlerde kullanıldığı için kalıcı olarak silinemedi, ancak listeden kaldırıldı.";
                break;
            default:
                TempData["Error"] = "Ürün bulunamadı.";
                break;
        }
        return RedirectToAction("Index");
    }

    [HttpPost("durum/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        await _productService.ToggleProductStatusAsync(id);
        return RedirectToAction("Index");
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin")]
[Route("admin/kategoriler")]
public class CategoryAdminController : Controller
{
    private readonly CategoryService _categoryService;
    private readonly ISlugService _slugService;

    public CategoryAdminController(CategoryService categoryService, ISlugService slugService)
    {
        _categoryService = categoryService;
        _slugService = slugService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var categories = await _categoryService.GetAllCategoriesAsync();
        return View(categories);
    }

    [HttpGet("ekle")]
    public async Task<IActionResult> Create()
    {
        var vm = new AdminCategoryViewModel
        {
            ParentCategories = await _categoryService.GetActiveCategoriesAsync()
        };
        return View(vm);
    }

    [HttpPost("ekle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminCategoryViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.ParentCategories = await _categoryService.GetActiveCategoriesAsync();
            return View(model);
        }

        var slug = await _slugService.GenerateUniqueSlugAsync(model.Name, "category");
        await _categoryService.CreateCategoryAsync(new Core.Entities.Category
        {
            Name = model.Name,
            Slug = slug,
            Description = model.Description,
            ParentId = model.ParentId,
            IsActive = model.IsActive,
            DisplayOrder = model.DisplayOrder
        });

        TempData["Success"] = "Kategori eklendi.";
        return RedirectToAction("Index");
    }

    [HttpGet("duzenle/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var category = await _categoryService.GetCategoryByIdAsync(id);
        if (category is null) return NotFound();

        // Ekleme/düzenleme aynı formu (Create.cshtml, Model.Id ile mod ayrımı) kullanır.
        // Ayrı bir Edit.cshtml'de bu view'i partial olarak render etmek layout'u iki kez
        // uyguluyordu (partial içinde set edilen Layout da çalışır) — o yüzden direkt bu view.
        return View("Create", new AdminCategoryViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            ParentId = category.ParentId,
            IsActive = category.IsActive,
            DisplayOrder = category.DisplayOrder,
            ImageUrl = category.ImageUrl,
            ParentCategories = await _categoryService.GetActiveCategoriesAsync()
        });
    }

    [HttpPost("duzenle/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminCategoryViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.ParentCategories = await _categoryService.GetActiveCategoriesAsync();
            return View("Create", model);
        }

        var category = await _categoryService.GetCategoryByIdAsync(id);
        if (category is null) return NotFound();

        category.Name = model.Name;
        category.Description = model.Description;
        category.ParentId = model.ParentId;
        category.IsActive = model.IsActive;
        category.DisplayOrder = model.DisplayOrder;
        await _categoryService.UpdateCategoryAsync(category);

        TempData["Success"] = "Kategori güncellendi.";
        return RedirectToAction("Index");
    }

    [HttpPost("sil/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _categoryService.DeleteCategoryAsync(id);
        TempData["Success"] = "Kategori silindi.";
        return RedirectToAction("Index");
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin")]
[Route("admin/ozel-gunler")]
public class OccasionAdminController : Controller
{
    private readonly OccasionService _occasionService;
    private readonly ISlugService _slugService;

    public OccasionAdminController(OccasionService occasionService, ISlugService slugService)
    {
        _occasionService = occasionService;
        _slugService = slugService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var occasions = await _occasionService.GetAllOccasionsAsync();
        return View(occasions);
    }

    [HttpGet("ekle")]
    public IActionResult Create() => View(new AdminOccasionViewModel());

    [HttpPost("ekle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminOccasionViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var slug = await _slugService.GenerateUniqueSlugAsync(model.Name, "occasion");
        await _occasionService.CreateOccasionAsync(new Core.Entities.Occasion
        {
            Name = model.Name,
            Slug = slug,
            IsActive = model.IsActive,
            DisplayOrder = model.DisplayOrder
        });

        TempData["Success"] = "Özel gün eklendi.";
        return RedirectToAction("Index");
    }

    [HttpGet("duzenle/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var occasion = await _occasionService.GetOccasionByIdAsync(id);
        if (occasion is null) return NotFound();

        // CategoryAdmin ile aynı desen: ekleme/düzenleme tek formu (Create.cshtml) paylaşır.
        return View("Create", new AdminOccasionViewModel
        {
            Id = occasion.Id,
            Name = occasion.Name,
            IsActive = occasion.IsActive,
            DisplayOrder = occasion.DisplayOrder
        });
    }

    [HttpPost("duzenle/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminOccasionViewModel model)
    {
        if (!ModelState.IsValid)
            return View("Create", model);

        var occasion = await _occasionService.GetOccasionByIdAsync(id);
        if (occasion is null) return NotFound();

        occasion.Name = model.Name;
        occasion.IsActive = model.IsActive;
        occasion.DisplayOrder = model.DisplayOrder;
        await _occasionService.UpdateOccasionAsync(occasion);

        TempData["Success"] = "Özel gün güncellendi.";
        return RedirectToAction("Index");
    }

    [HttpPost("sil/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _occasionService.DeleteOccasionAsync(id);
        TempData["Success"] = "Özel gün silindi.";
        return RedirectToAction("Index");
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin")]
[Route("admin/bolgeler")]
public class DeliveryZoneAdminController : Controller
{
    private readonly FethiyeCicekcisi.Core.Interfaces.Repositories.IDeliveryZoneRepository _zoneRepo;

    public DeliveryZoneAdminController(FethiyeCicekcisi.Core.Interfaces.Repositories.IDeliveryZoneRepository zoneRepo)
        => _zoneRepo = zoneRepo;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var zones = (await _zoneRepo.FindAsync(z => !z.IsDeleted))
            .OrderBy(z => z.DisplayOrder).ThenBy(z => z.Name);
        return View(zones);
    }

    [HttpGet("ekle")]
    public IActionResult Create() => View(new AdminDeliveryZoneViewModel());

    [HttpPost("ekle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminDeliveryZoneViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        await _zoneRepo.AddAsync(new DeliveryZone
        {
            City = model.City.Trim(),
            Name = model.Name.Trim(),
            Fee = model.Fee,
            DisplayOrder = model.DisplayOrder,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        });
        await _zoneRepo.SaveChangesAsync();

        TempData["Success"] = "Teslimat bölgesi eklendi.";
        return RedirectToAction("Index");
    }

    [HttpGet("duzenle/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var zone = await _zoneRepo.GetByIdAsync(id);
        if (zone is null) return NotFound();

        // CategoryAdmin ile aynı desen: ekleme/düzenleme tek formu (Create.cshtml) paylaşır.
        return View("Create", new AdminDeliveryZoneViewModel
        {
            Id = zone.Id,
            City = zone.City,
            Name = zone.Name,
            Fee = zone.Fee,
            DisplayOrder = zone.DisplayOrder,
            IsActive = zone.IsActive
        });
    }

    [HttpPost("duzenle/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminDeliveryZoneViewModel model)
    {
        if (!ModelState.IsValid) return View("Create", model);

        var zone = await _zoneRepo.GetByIdAsync(id);
        if (zone is null) return NotFound();

        zone.City = model.City.Trim();
        zone.Name = model.Name.Trim();
        zone.Fee = model.Fee;
        zone.DisplayOrder = model.DisplayOrder;
        zone.IsActive = model.IsActive;
        zone.UpdatedAt = DateTime.UtcNow;
        _zoneRepo.Update(zone);
        await _zoneRepo.SaveChangesAsync();

        TempData["Success"] = "Teslimat bölgesi güncellendi.";
        return RedirectToAction("Index");
    }

    // Ekrandaki "Geçici Olarak Sil" — pasife çeker, listede kalır, sitede görünmez.
    [HttpPost("pasif/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var zone = await _zoneRepo.GetByIdAsync(id);
        if (zone is null) return NotFound();
        zone.IsActive = !zone.IsActive;
        zone.UpdatedAt = DateTime.UtcNow;
        _zoneRepo.Update(zone);
        await _zoneRepo.SaveChangesAsync();
        TempData["Success"] = zone.IsActive ? "Bölge aktifleştirildi." : "Bölge geçici olarak kapatıldı.";
        return RedirectToAction("Index");
    }

    // "Kalıcı Sil" — soft delete; admin listesinden de kalkar.
    [HttpPost("sil/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var zone = await _zoneRepo.GetByIdAsync(id);
        if (zone is null) return NotFound();
        zone.IsDeleted = true;
        _zoneRepo.Update(zone);
        await _zoneRepo.SaveChangesAsync();
        TempData["Success"] = "Bölge silindi.";
        return RedirectToAction("Index");
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin")]
[Route("admin/promosyon-kodlari")]
public class PromoCodeAdminController : Controller
{
    private readonly FethiyeCicekcisi.Core.Interfaces.Repositories.IPromoCodeRepository _promoRepo;

    public PromoCodeAdminController(FethiyeCicekcisi.Core.Interfaces.Repositories.IPromoCodeRepository promoRepo)
        => _promoRepo = promoRepo;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var codes = (await _promoRepo.FindAsync(p => !p.IsDeleted))
            .OrderByDescending(p => p.CreatedAt);
        return View(codes);
    }

    [HttpGet("ekle")]
    public IActionResult Create() =>
        // Ekrandaki gibi rastgele 6 haneli kod önerilir; admin isterse değiştirir.
        View(new AdminPromoCodeViewModel { Code = GenerateCode() });

    [HttpPost("ekle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminPromoCodeViewModel model)
    {
        model.Code = model.Code.Trim().ToUpperInvariant();
        if (ModelState.IsValid && await _promoRepo.FirstOrDefaultAsync(p => p.Code == model.Code) is not null)
            ModelState.AddModelError(nameof(model.Code), "Bu kod zaten mevcut.");
        if (!ModelState.IsValid) return View(model);

        await _promoRepo.AddAsync(new PromoCode
        {
            Code = model.Code,
            DiscountType = model.DiscountType,
            Amount = model.Amount,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        });
        await _promoRepo.SaveChangesAsync();

        TempData["Success"] = $"Promosyon kodu oluşturuldu: {model.Code}";
        return RedirectToAction("Index");
    }

    [HttpGet("duzenle/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var promo = await _promoRepo.GetByIdAsync(id);
        if (promo is null) return NotFound();

        return View("Create", new AdminPromoCodeViewModel
        {
            Id = promo.Id,
            Code = promo.Code,
            DiscountType = promo.DiscountType,
            Amount = promo.Amount,
            IsActive = promo.IsActive
        });
    }

    [HttpPost("duzenle/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminPromoCodeViewModel model)
    {
        model.Code = model.Code.Trim().ToUpperInvariant();
        if (ModelState.IsValid && await _promoRepo.FirstOrDefaultAsync(p => p.Code == model.Code && p.Id != id) is not null)
            ModelState.AddModelError(nameof(model.Code), "Bu kod zaten mevcut.");
        if (!ModelState.IsValid) return View("Create", model);

        var promo = await _promoRepo.GetByIdAsync(id);
        if (promo is null) return NotFound();

        promo.Code = model.Code;
        promo.DiscountType = model.DiscountType;
        promo.Amount = model.Amount;
        promo.IsActive = model.IsActive;
        promo.UpdatedAt = DateTime.UtcNow;
        _promoRepo.Update(promo);
        await _promoRepo.SaveChangesAsync();

        TempData["Success"] = "Promosyon kodu güncellendi.";
        return RedirectToAction("Index");
    }

    [HttpPost("sil/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var promo = await _promoRepo.GetByIdAsync(id);
        if (promo is null) return NotFound();
        promo.IsDeleted = true;
        promo.IsActive = false;
        _promoRepo.Update(promo);
        await _promoRepo.SaveChangesAsync();
        TempData["Success"] = "Promosyon kodu silindi.";
        return RedirectToAction("Index");
    }

    private static string GenerateCode() =>
        Convert.ToHexString(Guid.NewGuid().ToByteArray()[..3]).ToUpperInvariant();
}

[Area("Admin")]
[Authorize(Roles = "Admin")]
[Route("admin/siparisler")]
public class OrderAdminController : Controller
{
    private readonly OrderService _orderService;

    public OrderAdminController(OrderService orderService) => _orderService = orderService;

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, string? durum = null)
    {
        var (orders, total, pages) = await _orderService.GetPagedOrdersAsync(page, 20, durum);
        var vm = new AdminOrderListViewModel
        {
            Orders = orders,
            CurrentPage = page,
            TotalPages = pages,
            StatusFilter = durum
        };
        return View(vm);
    }

    [HttpGet("detay/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order is null) return NotFound();
        return View(order);
    }

    [HttpPost("durum/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status, string? takipNo = null)
    {
        await _orderService.UpdateOrderStatusAsync(id, status, takipNo);
        TempData["Success"] = "Sipariş durumu güncellendi.";
        return RedirectToAction("Detail", new { id });
    }
}
