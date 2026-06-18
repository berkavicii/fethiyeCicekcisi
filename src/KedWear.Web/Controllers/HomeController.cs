using KedWear.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace KedWear.Web.Controllers;

public class HomeController : Controller
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;

    public HomeController(ProductService productService, CategoryService categoryService)
    {
        _productService = productService;
        _categoryService = categoryService;
    }

    public async Task<IActionResult> Index()
    {
        var featured = await _productService.GetFeaturedProductsAsync(8);
        var categories = await _categoryService.GetActiveCategoriesAsync();
        ViewBag.FeaturedProducts = featured;
        ViewBag.Categories = categories;
        return View();
    }

    [Route("hakkimizda")]
    public IActionResult About() => View();

    [Route("iletisim")]
    public IActionResult Contact() => View();

    [Route("hata")]
    public IActionResult Error() => View();
}
