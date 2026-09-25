using Microsoft.AspNetCore.Mvc;

namespace FethiyeCicekcisi.Web.Controllers;

/// <summary>Statik yasal metin sayfaları (ödeme kuruluşu başvurusu ve mevzuat gereği).
/// İçerikler admin panelinden düzenlenmiyor — sabit Razor view'lar; güncelleme gerektiğinde
/// ilgili .cshtml dosyası doğrudan düzenlenir.</summary>
public class LegalController : Controller
{
    [Route("mesafeli-satis-sozlesmesi")]
    public IActionResult DistanceSalesContract() => View();

    [Route("uyelik-sozlesmesi")]
    public IActionResult MembershipAgreement() => View();

    [Route("iptal-iade-degisim-politikasi")]
    public IActionResult CancellationRefundPolicy() => View();

    [Route("gizlilik-politikasi")]
    public IActionResult PrivacyPolicy() => View();

    [Route("kvkk-aydinlatma-metni")]
    public IActionResult KvkkNotice() => View();

    [Route("kullanim-sartlari")]
    public IActionResult TermsOfUse() => View();

    [Route("cerez-politikasi")]
    public IActionResult CookiePolicy() => View();

    [Route("teslimat-kosullari")]
    public IActionResult ShippingPolicy() => View();

    [Route("gizlilik-ve-guvenlik")]
    public IActionResult PrivacySecurity() => View();
}
