namespace FethiyeCicekcisi.Core.Entities;

public class CartItem : BaseEntity
{
    public string? UserId { get; set; }
    public AppUser? User { get; set; }
    public string? SessionId { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }

    public decimal TotalPrice => UnitPrice * Quantity;
}
