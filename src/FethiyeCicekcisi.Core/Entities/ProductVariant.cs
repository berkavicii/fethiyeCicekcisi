namespace FethiyeCicekcisi.Core.Entities;

public class ProductVariant : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>Çiçek seçeneği: "11 Adet", "21 Adet", "Büyük Boy" gibi — stok ve
    /// fiyat farkı bu seçenek üzerinden takip edilir.</summary>
    public string? Size { get; set; }

    public int StockQuantity { get; set; } = 0;
    public decimal? PriceDifference { get; set; }
    public string? SKU { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
