using FethiyeCicekcisi.Application.Services;
using FethiyeCicekcisi.Web.ViewModels.Product;
using Microsoft.AspNetCore.Mvc;

namespace FethiyeCicekcisi.Web.Controllers;

[Route("urunler")]
public class ProductController : Controller
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;
    private readonly OccasionService _occasionService;

    public ProductController(ProductService productService, CategoryService categoryService, OccasionService occasionService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _occasionService = occasionService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        int page = 1,
        int? kategori = null,
        string? ara = null,
        string? sirala = null,
        string? secenek = null,
        int? ozelgun = null)
    {
        var (products, totalCount, totalPages) = await _productService.GetPagedProductsAsync(
            page, 12, kategori, ara, sirala, secenek, ozelgun);
        var categories = await _categoryService.GetActiveCategoriesAsync();
        var occasions = (await _occasionService.GetActiveOccasionsAsync()).ToList();
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
            Occasions = occasions,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalCount = totalCount,
            SelectedCategoryId = kategori,
            SelectedOccasionId = ozelgun,
            OccasionName = ozelgun.HasValue ? occasions.FirstOrDefault(o => o.Id == ozelgun.Value)?.Name : null,
            SearchTerm = ara,
            SortBy = sirala,
            SelectedSize = secenek,
            AvailableSizes = availableSizes,
            CategoryName = categoryName
        };

        return View(vm);
    }

    [HttpGet("kategori/{slug}")]
    public async Task<IActionResult> Category(string slug, int page = 1, string? sirala = null, string? secenek = null)
    {
        var category = await _categoryService.GetCategoryBySlugAsync(slug);
        if (category is null) return NotFound();

        var (products, totalCount, totalPages) = await _productService.GetPagedProductsAsync(
            page, 12, category.Id, null, sirala, secenek);
        var categories = await _categoryService.GetActiveCategoriesAsync();
        var occasions = await _occasionService.GetActiveOccasionsAsync();
        var availableSizes = await _productService.GetAvailableSizesAsync();

        var vm = new ProductListViewModel
        {
            Products = products,
            Categories = categories,
            Occasions = occasions,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalCount = totalCount,
            SelectedCategoryId = category.Id,
            CategoryName = category.Name,
            SortBy = sirala,
            SelectedSize = secenek,
            AvailableSizes = availableSizes
        };

        return View("Index", vm);
    }

    [HttpGet("ozel-gun/{slug}")]
    public async Task<IActionResult> Occasion(string slug, int page = 1, string? sirala = null, string? secenek = null)
    {
        var occasion = await _occasionService.GetOccasionBySlugAsync(slug);
        if (occasion is null) return NotFound();

        var (products, totalCount, totalPages) = await _productService.GetPagedProductsAsync(
            page, 12, null, null, sirala, secenek, occasion.Id);
        var categories = await _categoryService.GetActiveCategoriesAsync();
        var occasions = await _occasionService.GetActiveOccasionsAsync();
        var availableSizes = await _productService.GetAvailableSizesAsync();

        var vm = new ProductListViewModel
        {
            Products = products,
            Categories = categories,
            Occasions = occasions,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalCount = totalCount,
            SelectedOccasionId = occasion.Id,
            OccasionName = occasion.Name,
            SortBy = sirala,
            SelectedSize = secenek,
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
