namespace FethiyeCicekcisi.Core.Entities;

/// <summary>Ürün ↔ Özel gün many-to-many bağlantı tablosu (bileşik anahtar:
/// ProductId + OccasionId).</summary>
public class ProductOccasion
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int OccasionId { get; set; }
    public Occasion Occasion { get; set; } = null!;
}
