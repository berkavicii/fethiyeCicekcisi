using System.Globalization;

namespace FethiyeCicekcisi.Core.Entities;

/// <summary>ProductLocalizationExtensions ile aynı mantık — kategori adı/açıklamasını o an
/// seçili arayüz diline göre gösterir, çeviri yoksa orijinal Türkçe içeriğe düşer.</summary>
public static class CategoryLocalizationExtensions
{
    private static CategoryTranslation? CurrentTranslation(Category category)
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (lang == "tr") return null;
        return category.Translations.FirstOrDefault(t => t.LanguageCode == lang);
    }

    public static string GetLocalizedName(this Category category) =>
        CurrentTranslation(category)?.Name is { Length: > 0 } name ? name : category.Name;

    public static string? GetLocalizedDescription(this Category category) =>
        CurrentTranslation(category)?.Description is { Length: > 0 } d ? d : category.Description;
}
