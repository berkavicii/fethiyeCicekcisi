using FethiyeCicekcisi.Core.Enums;

namespace FethiyeCicekcisi.Core.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    /// <summary>Ürün kodu (ör. YC-0012) — boş bırakılırsa kayıt sonrası otomatik atanır.</summary>
    public string? Code { get; set; }
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
    public string? CareInstructions { get; set; }

    /// <summary>Ürünle birlikte vazo/saksı geliyor mu (buketlerde genellikle hayır,
    /// aranjman ve orkidelerde evet).</summary>
    public bool ContainsVase { get; set; } = false;

    /// <summary>Siparişe not kartı eklenebilir mi (çelenklerde genellikle kurdele
    /// yazısı kullanılır, kart eklenmez).</summary>
    public bool AllowsMessageCard { get; set; } = true;

    /// <summary>Aynı gün teslimata uygun mu — sepetteki tüm ürünler uygunsa teslimat
    /// tarihi bugünden başlar, aksi hâlde en erken yarın seçilebilir.</summary>
    public bool IsSameDayDelivery { get; set; } = false;

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<ProductOccasion> ProductOccasions { get; set; } = new List<ProductOccasion>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public decimal CurrentPrice => DiscountPrice.HasValue && DiscountPrice.Value < Price
        ? DiscountPrice.Value
        : Price;

    public bool IsOnSale => DiscountPrice.HasValue && DiscountPrice.Value < Price;
}
