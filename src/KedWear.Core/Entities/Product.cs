using KedWear.Core.Enums;

namespace KedWear.Core.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public string? MainImageUrl { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public ProductStatus Status { get; set; } = ProductStatus.Active;
    public bool IsFeatured { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;
    public string? Material { get; set; }
    public string? CareInstructions { get; set; }

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public decimal CurrentPrice => DiscountPrice.HasValue && DiscountPrice.Value < Price
        ? DiscountPrice.Value
        : Price;

    public bool IsOnSale => DiscountPrice.HasValue && DiscountPrice.Value < Price;
}
