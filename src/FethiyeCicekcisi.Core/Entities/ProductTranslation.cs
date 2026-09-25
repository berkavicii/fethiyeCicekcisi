namespace FethiyeCicekcisi.Core.Entities;

/// <summary>Bir ürünün tek bir dile çevrilmiş adı/açıklaması — admin panelinde elle girilir
/// (bkz. ProductAdminController, _TranslationTabs.cshtml). Orijinal Türkçe içerik hep
/// Product.Name/Description'da kalır; bu tablo yalnızca EN/RU/DE gibi ek diller içindir.</summary>
public class ProductTranslation : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>ISO 639-1 dil kodu — "en", "ru", "de".</summary>
    public string LanguageCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
}
