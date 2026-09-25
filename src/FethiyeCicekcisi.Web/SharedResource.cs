namespace FethiyeCicekcisi.Web;

/// <summary>IStringLocalizer&lt;SharedResource&gt; için boş işaretçi sınıf — gerçek içerik
/// Resources/SharedResource.{culture}.resx dosyalarında. tr-TR için resx yok: IStringLocalizer,
/// hiçbir kültürde eşleşme bulamazsa anahtarın kendisini (yani Türkçe metni) döndürür.
///
/// Bilerek Resources/ klasörünün DIŞINDA duruyor: sınıf o klasörün içinde olsaydı, ASP.NET Core'un
/// resx arama kuralı hem tipin namespace'inden hem ResourcesPath ayarından "Resources" segmentini
/// ekleyip "Resources.Resources.SharedResource" gibi ikilenmiş bir isim arar ve hiçbir zaman
/// eşleşmez — tüm çeviriler sessizce Türkçe'ye düşerdi.</summary>
public class SharedResource;
