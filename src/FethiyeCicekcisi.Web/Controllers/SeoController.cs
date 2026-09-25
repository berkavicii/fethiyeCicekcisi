using System.Text;
using FethiyeCicekcisi.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FethiyeCicekcisi.Web.Controllers;

/// <summary>robots.txt ve sitemap.xml — statik dosya yerine controller action olarak üretilir
/// ki sitemap, ürün/kategori/özel gün listesi değiştikçe (yeni ürün eklendikçe) otomatik güncel
/// kalsın ve site URL'si appsettings'teki SiteSettings:SiteUrl'den tek yerden gelsin.</summary>
public class SeoController : Controller
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;
    private readonly OccasionService _occasionService;
    private readonly IConfiguration _config;

    public SeoController(
        ProductService productService,
        CategoryService categoryService,
        OccasionService occasionService,
        IConfiguration config)
    {
        _productService = productService;
        _categoryService = categoryService;
        _occasionService = occasionService;
        _config = config;
    }

    private string SiteUrl => (_config["SiteSettings:SiteUrl"] ?? "https://www.fethiyecicekcisi.com").TrimEnd('/');

    [Route("robots.txt")]
    public IActionResult Robots()
    {
        var sb = new StringBuilder();
        sb.AppendLine("User-agent: *");
        sb.AppendLine("Allow: /");
        // Alışveriş/hesap akışları ve admin panel arama sonuçlarında görünmesi gereken sayfalar
        // değil — indekslenmesinler diye crawl'dan da hariç tutulur.
        sb.AppendLine("Disallow: /admin/");
        sb.AppendLine("Disallow: /sepet");
        sb.AppendLine("Disallow: /siparis/");
        sb.AppendLine("Disallow: /hesap/");
        sb.AppendLine();
        sb.AppendLine($"Sitemap: {SiteUrl}/sitemap.xml");
        return Content(sb.ToString(), "text/plain", Encoding.UTF8);
    }

    [Route("sitemap.xml")]
    public async Task<IActionResult> Sitemap()
    {
        var categories = await _categoryService.GetActiveCategoriesAsync();
        var occasions = await _occasionService.GetActiveOccasionsAsync();
        var (products, _, _) = await _productService.GetPagedProductsAsync(1, 5000);

        var urls = new List<(string Loc, string ChangeFreq, string Priority)>
        {
            (SiteUrl + "/", "daily", "1.0"),
            (SiteUrl + "/urunler", "daily", "0.9"),
            (SiteUrl + "/hakkimizda", "monthly", "0.4"),
            (SiteUrl + "/iletisim", "monthly", "0.4"),
        };

        urls.AddRange(categories.Select(c => ($"{SiteUrl}/urunler/kategori/{c.Slug}", "daily", "0.8")));
        urls.AddRange(occasions.Select(o => ($"{SiteUrl}/urunler/ozel-gun/{o.Slug}", "weekly", "0.7")));
        urls.AddRange(products.Select(p => ($"{SiteUrl}/urunler/{p.Slug}", "weekly", "0.6")));

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        foreach (var (loc, changeFreq, priority) in urls)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{System.Net.WebUtility.HtmlEncode(loc)}</loc>");
            sb.AppendLine($"    <changefreq>{changeFreq}</changefreq>");
            sb.AppendLine($"    <priority>{priority}</priority>");
            sb.AppendLine("  </url>");
        }
        sb.AppendLine("</urlset>");

        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }
}
