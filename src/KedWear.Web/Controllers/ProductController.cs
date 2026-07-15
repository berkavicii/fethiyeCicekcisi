using KedWear.Application.Services;
using KedWear.Web.ViewModels.Product;
using Microsoft.AspNetCore.Mvc;

namespace KedWear.Web.Controllers;

[Route("urunler")]
public class ProductController : Controller
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;

    public ProductController(ProductService productService, CategoryService categoryService)
    {
        _productService = productService;
        _categoryService = categoryService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        int page = 1,
        int? kategori = null,
        string? ara = null,
        string? sirala = null,
        string? beden = null)
    {
        var (products, totalCount, totalPages) = await _productService.GetPagedProductsAsync(
            page, 12, kategori, ara, sirala, beden);
        var categories = await _categoryService.GetActiveCategoriesAsync();
        var availableSizes = await _productService.GetAvailableSizesAsync();

        string? categoryName = null;
        if (kategori.HasValue)
        {
            var cat = await _categoryService.GetCategoryByIdAsync(kategori.Value);
            categoryName = cat?.Name;
        }

        var vm = new ProductListViewModel
        {
            Products = products,
            Categories = categories,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalCount = totalCount,
            SelectedCategoryId = kategori,
            SearchTerm = ara,
            SortBy = sirala,
            SelectedSize = beden,
            AvailableSizes = availableSizes,
            CategoryName = categoryName
        };

        return View(vm);
    }

    [HttpGet("kategori/{slug}")]
    public async Task<IActionResult> Category(string slug, int page = 1, string? sirala = null, string? beden = null)
    {
        var category = await _categoryService.GetCategoryBySlugAsync(slug);
        if (category is null) return NotFound();

        var (products, totalCount, totalPages) = await _productService.GetPagedProductsAsync(
            page, 12, category.Id, null, sirala, beden);
        var categories = await _categoryService.GetActiveCategoriesAsync();
        var availableSizes = await _productService.GetAvailableSizesAsync();

        var vm = new ProductListViewModel
        {
            Products = products,
            Categories = categories,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalCount = totalCount,
            SelectedCategoryId = category.Id,
            CategoryName = category.Name,
            SortBy = sirala,
            SelectedSize = beden,
            AvailableSizes = availableSizes
        };

        return View("Index", vm);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Detail(string slug)
    {
        var product = await _productService.GetProductBySlugAsync(slug);
        if (product is null) return NotFound();

        var related = await _productService.GetPagedProductsAsync(1, 4, product.CategoryId);

        var vm = new ProductDetailViewModel
        {
            Product = product,
            RelatedProducts = related.Products.Where(p => p.Id != product.Id).Take(3)
        };

        return View(vm);
    }
}
