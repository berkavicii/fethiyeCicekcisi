using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Hosting;

namespace FethiyeCicekcisi.Application.Services;

/// <summary>Cloudflare R2 bağlantı ayarları. Değerler appsettings'e düz yazılmaz;
/// ortam değişkeni (R2__AccountId vb.) veya user-secrets üzerinden sağlanır.</summary>
public class R2StorageOptions
{
    public const string SectionName = "R2";

    public string AccountId { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;

    /// <summary>Bucket'ın herkese açık okuma adresi (r2.dev subdomain'i veya custom domain).
    /// Yüklenen görselin DB'ye yazılan URL'si bu taban + nesne anahtarından oluşur.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    public string ServiceUrl => $"https://{AccountId}.r2.cloudflarestorage.com";

    /// <summary>Beş değerin tamamı gelmeden R2 backend'i devreye girmez; eksik config ile
    /// yarım çalışıp (örn. public URL'siz kırık linkler üretip) sessizce bozulmasın diye.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountId) &&
        !string.IsNullOrWhiteSpace(AccessKeyId) &&
        !string.IsNullOrWhiteSpace(SecretAccessKey) &&
        !string.IsNullOrWhiteSpace(BucketName) &&
        !string.IsNullOrWhiteSpace(PublicBaseUrl);
}

/// <summary>Cloudflare R2 (S3-uyumlu) storage backend. Görsel işleme hattı
/// <see cref="ImageFileServiceBase"/>'ten aynen gelir; burada yalnızca son adım değişir:
/// işlenmiş görsel diske değil R2 bucket'ına PutObject ile yazılır ve DB'ye kaydedilecek
/// public URL döndürülür.</summary>
public class R2FileService : ImageFileServiceBase
{
    private readonly IAmazonS3 _s3;
    private readonly R2StorageOptions _options;
    private readonly IWebHostEnvironment _env;

    public R2FileService(IAmazonS3 s3, R2StorageOptions options, IWebHostEnvironment env)
    {
        _s3 = s3;
        _options = options;
        _env = env;
    }

    protected override async Task<string> StoreProcessedImageAsync(Stream processedImage, string contentType, string folder, string fileName)
    {
        var key = $"{folder}/{fileName}";
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = processedImage,
            ContentType = contentType,
            // R2, SDK'nın varsayılan aws-chunked/imzalı streaming gövdesini reddediyor;
            // HTTPS üzerinde olduğumuz için payload imzasını kapatmak güvenli ve gerekli.
            DisablePayloadSigning = true,
        };
        await _s3.PutObjectAsync(request);

        return $"{_options.PublicBaseUrl.TrimEnd('/')}/{key}";
    }

    public override async Task DeleteImageAsync(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return;

        // R2 öncesinden kalan site-relative URL'ler ("/uploads/...") hâlâ diskte yaşıyor;
        // ürün silinirken onların dosyası da diskten temizlenmeli.
        if (!imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var filePath = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(filePath))
                File.Delete(filePath);
            return;
        }

        // Nesne anahtarı URL path'inin kendisi ("products/<guid>.jpg") — PublicBaseUrl
        // ileride custom domain'e taşınsa bile eski kayıtların anahtarı doğru çözülür.
        var key = new Uri(imageUrl).AbsolutePath.TrimStart('/');
        if (string.IsNullOrEmpty(key)) return;

        await _s3.DeleteObjectAsync(_options.BucketName, key);
    }
}
