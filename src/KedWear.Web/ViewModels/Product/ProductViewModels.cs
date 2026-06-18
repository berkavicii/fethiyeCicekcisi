using KedWear.Core.Entities;

namespace KedWear.Web.ViewModels.Product;

public class ProductListViewModel
{
    public IEnumerable<Core.Entities.Product> Products { get; set; } = new List<Core.Entities.Product>();
    public IEnumerable<Category> Categories { get; set; } = new List<Category>();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int? SelectedCategoryId { get; set; }
    public string? SearchTerm { get; set; }
    public string? SortBy { get; set; }
    public string? CategoryName { get; set; }
}

public class ProductDetailViewModel
{
    public Core.Entities.Product Product { get; set; } = null!;
    public IEnumerable<Core.Entities.Product> RelatedProducts { get; set; } = new List<Core.Entities.Product>();
}
