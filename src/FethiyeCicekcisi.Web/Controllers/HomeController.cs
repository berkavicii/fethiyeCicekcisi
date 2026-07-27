using FethiyeCicekcisi.Application.Services;
using FethiyeCicekcisi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FethiyeCicekcisi.Web.Controllers;

public class HomeController : Controller
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        ProductService productService,
        CategoryService categoryService,
        IEmailService emailService,
        IConfiguration config,
        ILogger<HomeController> logger)
    {
        _productService = productService;
        _categoryService = categoryService;
        _emailService = emailService;
        _config = config;
        _logger = logger;
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

    [HttpGet("iletisim")]
    public IActionResult Contact() => View();

    [HttpPost("iletisim")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(string name, string email, string message)
    {
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(message))
        {
            var to = _config["SiteSettings:ContactEmail"];
            if (!string.IsNullOrWhiteSpace(to))
            {
                try
                {
                    await _emailService.SendAsync(to, "Fethiye Çiçekçisi",
                        $"İletişim Formu — {name}",
                        $"<p><strong>Gönderen:</strong> {System.Net.WebUtility.HtmlEncode(name)} ({System.Net.WebUtility.HtmlEncode(email)})</p>" +
                        $"<p>{System.Net.WebUtility.HtmlEncode(message).Replace("\n", "<br/>")}</p>");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "İletişim formu e-postası gönderilemedi.");
                }
            }
            TempData["ContactSuccess"] = true;
        }
        return RedirectToAction("Contact");
    }

    [Route("hata")]
    public IActionResult Error() => View();
}
