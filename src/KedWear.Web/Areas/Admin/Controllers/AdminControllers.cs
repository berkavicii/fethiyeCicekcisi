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
            DisplayOrder = model.DisplayOrder
        };

        // Add variants
        foreach (var v in model.Variants.Where(v => !v.IsDeleted))
        {
            product.Variants.Add(new ProductVariant
            {
                Size = v.Size,
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
            foreach (var img in images)
            {
                if (!_fileService.IsValidImage(img.FileName, img.Length)) continue;
                await using var stream = img.OpenReadStream();
                var url = await _fileService.UploadImageAsync(stream, img.FileName);

                var productImage = new Core.Entities.ProductImage
                {
                    ProductId = product.Id,
                    ImageUrl = url,
                    AltText = product.Name,
                    IsMain = first,
                    DisplayOrder = first ? 0 : 1
                };
                product.Images.Add(productImage);

                if (first)
                {
                    product.MainImageUrl = url;
                    await _productService.UpdateProductAsync(product);
                }
                first = false;
            }
        }

        TempData["Success"] = "Ürün başarıyla eklendi.";
        return RedirectToAction("Index");
    }

    [HttpGet("duzenle/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _productService.GetProductByIdAsync(id);
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
            DisplayOrder = product.DisplayOrder,
            Categories = categories,
            ExistingImages = product.Images.ToList(),
            Variants = product.Variants.Select(v => new AdminVariantViewModel
            {
                Id = v.Id,
                Size = v.Size,
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
    public async Task<IActionResult> Edit(int id, AdminProductViewModel model, List<IFormFile>? newImages)
    {
        if (!ModelState.IsValid)
        {
            model.Categories = await _categoryService.GetActiveCategoriesAsync();
            var p = await _productService.GetProductByIdAsync(id);
            model.ExistingImages = p?.Images.ToList();
            return View(model);
        }

        var product = await _productService.GetProductByIdAsync(id);
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
        product.DisplayOrder = model.DisplayOrder;

        await _productService.UpdateProductAsync(product);

        if (newImages?.Any() == true)
        {
            bool hasMain = product.Images.Any(i => i.IsMain);
            foreach (var img in newImages)
            {
                if (!_fileService.IsValidImage(img.FileName, img.Length)) continue;
                await using var stream = img.OpenReadStream();
                var url = await _fileService.UploadImageAsync(stream, img.FileName);

                if (!hasMain)
                {
                    product.MainImageUrl = url;
                    hasMain = true;
                }
            }
            await _productService.UpdateProductAsync(product);
        }

        TempData["Success"] = "Ürün güncellendi.";
        return RedirectToAction("Index");
    }

    [HttpPost("sil/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _productService.DeleteProductAsync(id);
        TempData["Success"] = "Ürün silindi.";
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

        return View(new AdminCategoryViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            ParentId = category.ParentId,
            IsActive = category.IsActive,
            DisplayOrder = category.DisplayOrder,
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
            return View(model);
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
