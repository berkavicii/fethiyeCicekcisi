using KedWear.Core.Entities;

namespace KedWear.Web.ViewModels.Cart;

public class CartViewModel
{
    public IEnumerable<CartItem> Items { get; set; } = new List<CartItem>();
    public decimal SubTotal => Items.Sum(i => i.TotalPrice);
    public decimal ShippingCost { get; set; }
    public decimal Total => SubTotal + ShippingCost;
    public int ItemCount => Items.Sum(i => i.Quantity);
    public decimal FreeShippingThreshold { get; set; } = 500;
    public decimal RemainingForFreeShipping => Math.Max(0, FreeShippingThreshold - SubTotal);
}

public class AddToCartViewModel
{
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public string? PantSize { get; set; }
    public int Quantity { get; set; } = 1;
}
