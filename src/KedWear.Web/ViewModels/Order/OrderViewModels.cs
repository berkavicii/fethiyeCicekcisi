using System.ComponentModel.DataAnnotations;
using KedWear.Core.Entities;

namespace KedWear.Web.ViewModels.Order;

public class CheckoutViewModel
{
    public IEnumerable<CartItem> CartItems { get; set; } = new List<CartItem>();
    public decimal SubTotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal TotalAmount { get; set; }

    [Required(ErrorMessage = "Ad zorunludur.")]
    [Display(Name = "Ad")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Soyad zorunludur.")]
    [Display(Name = "Soyad")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefon zorunludur.")]
    [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
    [Display(Name = "Telefon")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Adres zorunludur.")]
    [Display(Name = "Adres")]
    public string AddressLine1 { get; set; } = string.Empty;

    [Display(Name = "Adres (Devam)")]
    public string? AddressLine2 { get; set; }

    [Required(ErrorMessage = "Şehir zorunludur.")]
    [Display(Name = "Şehir")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "İlçe zorunludur.")]
    [Display(Name = "İlçe")]
    public string District { get; set; } = string.Empty;

    [Display(Name = "Posta Kodu")]
    public string? ZipCode { get; set; }

    [Display(Name = "Sipariş Notu")]
    public string? Notes { get; set; }

    public IEnumerable<Address>? SavedAddresses { get; set; }
    public bool SaveAddress { get; set; }
}

public class OrderSummaryViewModel
{
    public Core.Entities.Order Order { get; set; } = null!;
    public string? PayTRIframeToken { get; set; }
}
