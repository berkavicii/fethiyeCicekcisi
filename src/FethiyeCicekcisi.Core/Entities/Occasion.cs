namespace FethiyeCicekcisi.Core.Entities;

/// <summary>Özel gün etiketi (Anneler Günü, Sevgililer Günü...). Bir ürün tek
/// kategoride durur ama birden çok özel güne etiketlenebilir (ProductOccasion).</summary>
public class Occasion : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public ICollection<ProductOccasion> ProductOccasions { get; set; } = new List<ProductOccasion>();
}
