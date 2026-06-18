namespace KedWear.Core.Entities;

public class ProductVariant : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string? Size { get; set; }
    public string? Color { get; set; }
    public string? ColorCode { get; set; }
    public int StockQuantity { get; set; } = 0;
    public decimal? PriceDifference { get; set; }
    public string? SKU { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
