using KedWear.Application.Services;
using KedWear.Core.Entities;
using KedWear.Core.Enums;
using KedWear.Core.Interfaces.Services;
using KedWear.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KedWear.Web.Areas.Admin.Controllers;

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
    private readonly IFileService _fileService;
    private readonly ISlugService _slugService;
    private readonly KedWear.Core.Interfaces.Repositories.IProductRepository _productRepo;
    private readonly KedWear.Core.Interfaces.Repositories.ICategoryRepository _categoryRepo;

    public ProductAdminController(
        ProductService productService,
        CategoryService categoryService,
        IFileService fileService,
        ISlugService slugService,
        KedWear.Core.Interfaces.Repositories.IProductRepository productRepo,
        KedWear.Core.Interfaces.Repositories.ICategoryRepository categoryRepo)
    {
        _productService = productService;
        _categoryService = categoryService;
        _fileService = fileService;
        _slugService = slugService;
        _productRepo = productRepo;
        _categoryRepo = categoryRepo;
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
        var categories = await _categoryService.GetActiveCategoriesAsync();
        return View(new AdminProductViewModel { Categories = categories });
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
            return View(model);
        }

        var slug = await _slugService.GenerateUniqueSlugAsync(model.Name, "product");

        var product = new Product
        {
            Name = model.Name,
            Slug = slug,
            Description = model.Description,
            ShortDescription = model.ShortDescription,
            Price = model.Price,
            DiscountPrice = model.DiscountPrice,
            CategoryId = model.CategoryId,
            IsFeatured = model.IsFeatured,
            Status = model.Status,
            Material = model.Material,
            CareInstructions = model.CareInstructions,
            MannequinMeasurements = model.MannequinMeasurements,
            DisplayOrder = model.DisplayOrder
        };

        // Add variants
        foreach (var v in model.Variants.Where(v => !v.IsDeleted))
        {
            product.Variants.Add(new ProductVariant
            {
                Size = v.Size,
                PantSize = v.PantSize,
                Color = v.Color,
                ColorCode = v.ColorCode,
                StockQuantity = v.StockQuantity,
                PriceDifference = v.PriceDifference,
                SKU = v.SKU,
                IsActive = v.IsActive
            });
        }

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

        var categories = await _categoryService.GetActiveCategoriesAsync();

        var vm = new AdminProductViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            ShortDescription = product.ShortDescription,
            Price = product.Price,
            DiscountPrice = product.DiscountPrice,
            CategoryId = product.CategoryId,
            IsFeatured = product.IsFeatured,
            Status = product.Status,
            Material = product.Material,
            CareInstructions = product.CareInstructions,
            MannequinMeasurements = product.MannequinMeasurements,
            DisplayOrder = product.DisplayOrder,
            Categories = categories,
            ExistingImages = product.Images.ToList(),
            Variants = product.Variants.Select(v => new AdminVariantViewModel
            {
                Id = v.Id,
                Size = v.Size,
                PantSize = v.PantSize,
                Color = v.Color,
                ColorCode = v.ColorCode,
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
            var p = await _productService.GetProductForAdminEditAsync(id);
            model.ExistingImages = p?.Images.ToList();
            return View(model);
        }

        var product = await _productService.GetProductForAdminEditAsync(id);
        if (product is null) return NotFound();

        product.Name = model.Name;
        product.Description = model.Description;
        product.ShortDescription = model.ShortDescription;
        product.Price = model.Price;
        product.DiscountPrice = model.DiscountPrice;
        product.CategoryId = model.CategoryId;
        product.IsFeatured = model.IsFeatured;
        product.Status = model.Status;
        product.Material = model.Material;
        product.CareInstructions = model.CareInstructions;
        product.MannequinMeasurements = model.MannequinMeasurements;
        product.DisplayOrder = model.DisplayOrder;

        foreach (var v in model.Variants)
        {
            if (v.Id == 0)
            {
                if (v.IsDeleted) continue; // added then removed again before submit — never existed
                product.Variants.Add(new ProductVariant
                {
                    ProductId = product.Id,
                    Size = v.Size,
                    PantSize = v.PantSize,
                    Color = v.Color,
                    ColorCode = v.ColorCode,
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
            existing.PantSize = v.PantSize;
            existing.Color = v.Color;
            existing.ColorCode = v.ColorCode;
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
