using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace FethiyeCicekcisi.Core.Enums;

public enum OrderStatus
{
    [Display(Name = "Beklemede")]
    Pending = 0,

    [Display(Name = "Ödeme Bekleniyor")]
    PaymentPending = 1,

    [Display(Name = "Ödeme Alındı")]
    PaymentSuccess = 2,

    [Display(Name = "Ödeme Başarısız")]
    PaymentFailed = 3,

    [Display(Name = "Hazırlanıyor")]
    Processing = 4,

    [Display(Name = "Kargoya Verildi")]
    Shipped = 5,

    [Display(Name = "Teslim Edildi")]
    Delivered = 6,

    [Display(Name = "İptal Edildi")]
    Cancelled = 7,

    [Display(Name = "İade Edildi")]
    Refunded = 8
}

/// <summary>Teslimat saat aralığı — yalnızca iki dilim satılır; 0 "seçilmedi"
/// anlamına gelir ve formda [Range(1,2)] ile reddedilir.</summary>
public enum DeliveryTimeSlot
{
    [Display(Name = "Seçiniz")]
    Belirtilmemis = 0,

    [Display(Name = "13:00 – 17:00")]
    Ogle_13_17 = 1,

    [Display(Name = "17:00 – 21:00")]
    Aksam_17_21 = 2
}

public enum PromoDiscountType
{
    [Display(Name = "Yüzde İndirim")]
    Percent = 0,

    [Display(Name = "Sabit Fiyat (TL)")]
    Fixed = 1
}

public enum PaymentStatus
{
    [Display(Name = "Beklemede")]
    Pending = 0,

    [Display(Name = "Başarılı")]
    Success = 1,

    [Display(Name = "Başarısız")]
    Failed = 2,

    [Display(Name = "İade Edildi")]
    Refunded = 3
}

public enum ProductStatus
{
    [Display(Name = "Aktif")]
    Active = 0,

    [Display(Name = "Pasif")]
    Inactive = 1,

    [Display(Name = "Stokta Yok")]
    OutOfStock = 2
}

/// <summary>Ürün silme işleminin sonucu. Geçmiş siparişlerde kullanılmış bir ürün, sipariş
/// geçmişini bozmamak için (OrderItem -> Product FK cascade delete) kalıcı silinemez.</summary>
public enum ProductDeleteResult
{
    NotFound,
    HardDeleted,
    SoftDeleted
}

public static class EnumDisplayExtensions
{
    /// <summary>[Display(Name)] etiketini döndürür; etiket yoksa enum adına düşer.</summary>
    public static string ToDisplayName(this Enum value) =>
        value.GetType()
             .GetMember(value.ToString())
             .FirstOrDefault()?
             .GetCustomAttribute<DisplayAttribute>()?
             .GetName() ?? value.ToString();
}
