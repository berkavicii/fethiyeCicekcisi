using FethiyeCicekcisi.Core.Entities;

namespace FethiyeCicekcisi.Web.ViewModels.Product;

public class ProductListViewModel
{
    public IEnumerable<Core.Entities.Product> Products { get; set; } = new List<Core.Entities.Product>();
    public IEnumerable<Category> Categories { get; set; } = new List<Category>();
    public IEnumerable<Occasion> Occasions { get; set; } = new List<Occasion>();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int? SelectedCategoryId { get; set; }
    public int? SelectedOccasionId { get; set; }
    public string? SearchTerm { get; set; }
    public string? SortBy { get; set; }
    public string? CategoryName { get; set; }
    public string? OccasionName { get; set; }
    public string? SelectedSize { get; set; }
    public IReadOnlyList<string> AvailableSizes { get; set; } = [];
}

public class ProductDetailViewModel
{
    public Core.Entities.Product Product { get; set; } = null!;
    public IEnumerable<Core.Entities.Product> RelatedProducts { get; set; } = new List<Core.Entities.Product>();

    /// <summary>Ürün sayfasında teslimat bölgesi/tarihi ön izlemesi için — Checkout'takiyle
    /// aynı aktif bölge listesi.</summary>
    public IEnumerable<DeliveryZone> Zones { get; set; } = new List<DeliveryZone>();

    /// <summary>Bu ürün aynı gün teslimata uygunsa bugün, değilse yarın.</summary>
    public DateOnly MinDeliveryDate { get; set; }
}
