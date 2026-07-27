using System.ComponentModel.DataAnnotations;
using FethiyeCicekcisi.Core.Entities;
using FethiyeCicekcisi.Core.Enums;

namespace FethiyeCicekcisi.Web.ViewModels.Admin;

public class AdminProductViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ürün adı zorunludur.")]
    [Display(Name = "Ürün Adı")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Ürün Kodu")]
    [MaxLength(30)]
    public string? Code { get; set; }

    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [Display(Name = "Kısa Açıklama")]
    [MaxLength(500)]
    public string? ShortDescription { get; set; }

    [Required(ErrorMessage = "Fiyat zorunludur.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Geçerli bir fiyat giriniz.")]
    [Display(Name = "Fiyat")]
    public decimal Price { get; set; }

    [Display(Name = "İndirimli Fiyat")]
    public decimal? DiscountPrice { get; set; }

    [Required(ErrorMessage = "Kategori seçiniz.")]
    [Display(Name = "Kategori")]
    public int CategoryId { get; set; }

    [Display(Name = "Öne Çıkan")]
    public bool IsFeatured { get; set; }

    [Display(Name = "Durum")]
    public ProductStatus Status { get; set; } = ProductStatus.Active;

    [Display(Name = "Bakım Bilgisi")]
    public string? CareInstructions { get; set; }

    [Display(Name = "Vazo/Saksı Dahil")]
    public bool ContainsVase { get; set; }

    [Display(Name = "Not Kartı Eklenebilir")]
    public bool AllowsMessageCard { get; set; } = true;

    [Display(Name = "Aynı Gün Teslimat")]
    public bool IsSameDayDelivery { get; set; }

    [Display(Name = "Sıralama")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Özel Günler")]
    public List<int> SelectedOccasionIds { get; set; } = new();

    public IEnumerable<Category>? Categories { get; set; }
    public IEnumerable<Occasion>? Occasions { get; set; }
    public List<AdminVariantViewModel> Variants { get; set; } = new();
    public List<ProductImage>? ExistingImages { get; set; }
}

public class AdminVariantViewModel
{
    public int Id { get; set; }

    /// <summary>Seçenek adı: "11 Adet", "21 Adet", "Büyük Boy"...</summary>
    public string? Size { get; set; }
    public int StockQuantity { get; set; }
    public decimal? PriceDifference { get; set; }
    public string? SKU { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
}

public class AdminCategoryViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Kategori adı zorunludur.")]
    [Display(Name = "Kategori Adı")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [Display(Name = "Üst Kategori")]
    public int? ParentId { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Sıralama")]
    public int DisplayOrder { get; set; }

    public string? ImageUrl { get; set; }

    public IEnumerable<Category>? ParentCategories { get; set; }
}

public class AdminOccasionViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Özel gün adı zorunludur.")]
    [Display(Name = "Özel Gün Adı")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Sıralama")]
    public int DisplayOrder { get; set; }
}

public class AdminDeliveryZoneViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "İl zorunludur.")]
    [Display(Name = "İl")]
    public string City { get; set; } = "Fethiye";

    [Required(ErrorMessage = "Bölge adı zorunludur.")]
    [Display(Name = "İlçe/Semt/Bölge")]
    public string Name { get; set; } = string.Empty;

    [Range(0, 100000, ErrorMessage = "Geçerli bir ücret giriniz.")]
    [Display(Name = "Yol Ücreti (TL)")]
    public decimal Fee { get; set; }

    [Display(Name = "Sıra")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}

public class AdminPromoCodeViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Kod zorunludur.")]
    [MaxLength(30)]
    [Display(Name = "Promosyon Kodu")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "İndirim Türü")]
    public PromoDiscountType DiscountType { get; set; } = PromoDiscountType.Percent;

    [Range(0.01, 100000, ErrorMessage = "Geçerli bir miktar giriniz.")]
    [Display(Name = "İndirim Miktarı")]
    public decimal Amount { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}

public class AdminStatsViewModel
{
    public IReadOnlyList<FethiyeCicekcisi.Core.Models.ProductSalesStat> Stats { get; set; } = [];
    /// <summary>Seçili dönem filtresi (gün sayısı: "30", "90", "365"); null = tüm zamanlar.</summary>
    public string? Period { get; set; }

    public int TotalUnitsSold => Stats.Sum(s => s.UnitsSold);
    public decimal TotalRevenue => Stats.Sum(s => s.Revenue);
    public int TotalUnitsRefunded => Stats.Sum(s => s.UnitsRefunded);
    public double OverallRefundRate => TotalUnitsSold + TotalUnitsRefunded == 0
        ? 0
        : (double)TotalUnitsRefunded / (TotalUnitsSold + TotalUnitsRefunded);
}

public class AdminOrderListViewModel
{
    public IEnumerable<FethiyeCicekcisi.Core.Entities.Order> Orders { get; set; } = new List<FethiyeCicekcisi.Core.Entities.Order>();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public string? StatusFilter { get; set; }
}
