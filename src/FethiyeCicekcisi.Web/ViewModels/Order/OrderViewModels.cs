using System.ComponentModel.DataAnnotations;
using FethiyeCicekcisi.Core.Entities;
using FethiyeCicekcisi.Core.Enums;

namespace FethiyeCicekcisi.Web.ViewModels.Order;

public class CheckoutViewModel
{
    public IEnumerable<CartItem> CartItems { get; set; } = new List<CartItem>();
    public decimal SubTotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal TotalAmount { get; set; }

    // ---- Gönderen (siparişi veren) — üyeliksiz siparişte de zorunlu ----
    [Required(ErrorMessage = "Ad soyad zorunludur.")]
    [Display(Name = "Adınız Soyadınız")]
    public string SenderName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefon zorunludur.")]
    [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
    [Display(Name = "Telefonunuz")]
    public string SenderPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [Display(Name = "E-posta Adresiniz")]
    public string SenderEmail { get; set; } = string.Empty;

    [Display(Name = "Gönderen bilgim gizli kalsın")]
    public bool IsAnonymousSender { get; set; }

    // ---- Alıcı (çiçeğin teslim edileceği kişi) ----
    [Required(ErrorMessage = "Alıcı adı zorunludur.")]
    [Display(Name = "Alıcı Adı")]
    public string RecipientFirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Alıcı soyadı zorunludur.")]
    [Display(Name = "Alıcı Soyadı")]
    public string RecipientLastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Alıcı telefonu zorunludur.")]
    [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
    [Display(Name = "Alıcı Telefonu")]
    public string RecipientPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Teslimat adresi zorunludur.")]
    [Display(Name = "Teslimat Adresi")]
    public string RecipientAddressLine1 { get; set; } = string.Empty;

    [Display(Name = "Adres (Devam)")]
    public string? RecipientAddressLine2 { get; set; }

    [Required(ErrorMessage = "Teslimat bölgesi seçiniz.")]
    [Display(Name = "Teslimat Bölgesi")]
    public int? DeliveryZoneId { get; set; }

    // ---- Teslimat planı ----
    [Required(ErrorMessage = "Teslimat tarihi seçiniz.")]
    [Display(Name = "Teslimat Tarihi")]
    public DateOnly? DeliveryDate { get; set; }

    [Range(1, 2, ErrorMessage = "Teslimat saati seçiniz.")]
    [Display(Name = "Teslimat Saati")]
    public DeliveryTimeSlot DeliveryTimeSlot { get; set; } = DeliveryTimeSlot.Belirtilmemis;

    [MaxLength(200, ErrorMessage = "Kart mesajı en fazla 200 karakter olabilir.")]
    [Display(Name = "Kart Mesajı (opsiyonel)")]
    public string? CardMessage { get; set; }

    [Display(Name = "Promosyon Kodu")]
    public string? PromoCode { get; set; }

    [Display(Name = "Sipariş Notu")]
    public string? Notes { get; set; }

    public IEnumerable<DeliveryZone> Zones { get; set; } = new List<DeliveryZone>();

    /// <summary>Sepetteki TÜM ürünler aynı gün teslimata uygunsa bugün, değilse yarın —
    /// tarih seçicinin alt sınırı. Controller doldurur, POST'ta yeniden hesaplanır.</summary>
    public DateOnly MinDeliveryDate { get; set; }

    /// <summary>Sepette not kartı kabul eden en az bir ürün varsa kart mesajı alanı gösterilir.</summary>
    public bool AnyItemAllowsMessageCard { get; set; }
}

public class OrderSummaryViewModel
{
    public Core.Entities.Order Order { get; set; } = null!;
    public string? PayTRIframeToken { get; set; }
}
