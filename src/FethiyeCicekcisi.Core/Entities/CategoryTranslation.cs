namespace FethiyeCicekcisi.Core.Entities;

/// <summary>Bir kategorinin tek bir dile çevrilmiş adı/açıklaması — admin panelinde ürün
/// çevirisiyle aynı mantıkla elle girilir (bkz. ProductTranslation). Orijinal Türkçe içerik
/// hep Category.Name/Description'da kalır.</summary>
public class CategoryTranslation : BaseEntity
{
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    /// <summary>ISO 639-1 dil kodu — "en", "ru", "de".</summary>
    public string LanguageCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
