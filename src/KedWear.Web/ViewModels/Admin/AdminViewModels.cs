using System.ComponentModel.DataAnnotations;
using KedWear.Core.Entities;
using KedWear.Core.Enums;

namespace KedWear.Web.ViewModels.Admin;

public class AdminProductViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ürün adı zorunludur.")]
    [Display(Name = "Ürün Adı")]
    public string Name { get; set; } = string.Empty;

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

    [Display(Name = "Kumaş/Malzeme")]
    public string? Material { get; set; }

    [Display(Name = "Bakım Talimatları")]
    public string? CareInstructions { get; set; }

    [Display(Name = "Sıralama")]
    public int DisplayOrder { get; set; }

    public IEnumerable<Category>? Categories { get; set; }
    public List<AdminVariantViewModel> Variants { get; set; } = new();
    public List<ProductImage>? ExistingImages { get; set; }
}

public class AdminVariantViewModel
{
    public int Id { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
    public string? ColorCode { get; set; }
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

public class AdminOrderListViewModel
{
    public IEnumerable<KedWear.Core.Entities.Order> Orders { get; set; } = new List<KedWear.Core.Entities.Order>();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public string? StatusFilter { get; set; }
}
