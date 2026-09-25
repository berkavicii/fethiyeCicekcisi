using System.Globalization;

namespace FethiyeCicekcisi.Core.Entities;

/// <summary>Ürün adı/açıklamasını o an seçili arayüz diline göre gösterir — admin panelinde o
/// dil için elle girilmiş bir çeviri varsa onu, yoksa (henüz çevrilmemişse) orijinal Türkçe
/// içeriği döndürür. Sipariş/sepet/admin gibi Türkçe kalması gereken yerlerde bu metotlar
/// KULLANILMAZ — yalnızca müşteriye gösterilen katalog sayfalarında.</summary>
public static class ProductLocalizationExtensions
{
    private static ProductTranslation? CurrentTranslation(Product product)
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (lang == "tr") return null;
        return product.Translations.FirstOrDefault(t => t.LanguageCode == lang);
    }

    public static string GetLocalizedName(this Product product) =>
        CurrentTranslation(product)?.Name is { Length: > 0 } name ? name : product.Name;

    public static string? GetLocalizedShortDescription(this Product product) =>
        CurrentTranslation(product)?.ShortDescription is { Length: > 0 } d ? d : product.ShortDescription;

    public static string? GetLocalizedDescription(this Product product) =>
        CurrentTranslation(product)?.Description is { Length: > 0 } d ? d : product.Description;
}
