using FethiyeCicekcisi.Core.Interfaces.Repositories;
using FethiyeCicekcisi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace FethiyeCicekcisi.Application.Services;

/// <summary>Shared image pipeline (validation, HEIC detection, resize + re-encode via
/// ImageSharp). Where the processed bytes end up is the storage backend's job: see
/// <see cref="FileService"/> (local disk) and <see cref="R2FileService"/> (Cloudflare R2).</summary>
public abstract class ImageFileServiceBase : IFileService
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    // Modern phone cameras routinely produce 5-10MB JPEGs; the server resizes/recompresses
    // on upload anyway (see UploadImageAsync), so a generous input ceiling costs nothing.
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const int MaxDimension = 1600;

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName, string folder = "products")
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return await ProcessAndStoreAsync(fileStream, ext, folder);
    }

    public async Task<string> ImportLocalImageAsync(string sourceFilePath, string folder = "products")
    {
        var ext = Path.GetExtension(sourceFilePath).ToLowerInvariant();
        await using var stream = File.OpenRead(sourceFilePath);
        return await ProcessAndStoreAsync(stream, ext, folder);
    }

    private async Task<string> ProcessAndStoreAsync(Stream fileStream, string ext, string folder)
    {
        var uniqueName = $"{Guid.NewGuid()}{ext}";

        using var image = await Image.LoadAsync(fileStream);

        if (image.Width > MaxDimension || image.Height > MaxDimension)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxDimension, MaxDimension)
            }));
        }

        var encoder = GetEncoderFor(ext);
        using var processed = new MemoryStream();
        await image.SaveAsync(processed, encoder);
        processed.Position = 0;

        return await StoreProcessedImageAsync(processed, GetContentTypeFor(ext), folder, uniqueName);
    }

    /// <summary>Persists the already-processed image bytes and returns the URL that will be
    /// stored in the database (site-relative for disk, absolute public URL for R2).</summary>
    protected abstract Task<string> StoreProcessedImageAsync(Stream processedImage, string contentType, string folder, string fileName);

    public abstract Task DeleteImageAsync(string imageUrl);

    private static SixLabors.ImageSharp.Formats.IImageEncoder GetEncoderFor(string ext) => ext switch
    {
        ".jpg" or ".jpeg" => new JpegEncoder { Quality = 82 },
        ".webp" => new WebpEncoder { Quality = 82 },
        ".gif" => new GifEncoder(),
        _ => new PngEncoder()
    };

    private static string GetContentTypeFor(string ext) => ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "image/png"
    };

    public bool IsValidImage(string fileName, long fileSize) =>
        GetImageValidationError(fileName, fileSize) is null;

    public string? GetImageValidationError(string fileName, long fileSize)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return $"desteklenmeyen dosya formatı ({(string.IsNullOrEmpty(ext) ? "uzantısız" : ext)})";
        if (fileSize <= 0)
            return "dosya boş görünüyor";
        if (fileSize > MaxFileSizeBytes)
            return $"dosya boyutu {MaxFileSizeBytes / 1024 / 1024}MB'ı aşıyor ({fileSize / 1024.0 / 1024.0:0.0}MB)";
        return null;
    }

    public bool IsValidImageContent(Stream fileStream)
    {
        var header = ReadHeader(fileStream, 12);
        if (header is null) return false;

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return true;
        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return true;
        // GIF: "GIF87a" or "GIF89a"
        if (header.Length >= 6 && header[0] == 'G' && header[1] == 'I' && header[2] == 'F') return true;
        // WEBP: "RIFF"....."WEBP"
        if (header.Length >= 12 && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
            && header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P') return true;

        return false;
    }

    private static readonly string[] HeicBrands = ["heic", "heix", "heim", "heis", "hevc", "hevm", "hevs", "mif1", "msf1"];

    /// <summary>Detects HEIC/HEIF content by its ISO-BMFF "ftyp" box, independent of the
    /// file's extension — some iOS share/export paths label a still-HEIC file ".jpg", so an
    /// extension-only check misses it. This is what lets the server tell an admin "this really
    /// is a HEIC file the browser failed to convert" instead of a generic "corrupt file".</summary>
    public bool IsHeicContent(Stream fileStream)
    {
        var header = ReadHeader(fileStream, 12);
        if (header is null || header.Length < 12) return false;
        if (header[4] != 'f' || header[5] != 't' || header[6] != 'y' || header[7] != 'p') return false;
        var brand = System.Text.Encoding.ASCII.GetString(header, 8, 4).ToLowerInvariant();
        return HeicBrands.Contains(brand);
    }

    private static byte[]? ReadHeader(Stream fileStream, int length)
    {
        if (!fileStream.CanSeek) return null;
        fileStream.Position = 0;
        var header = new byte[length];
        var read = fileStream.Read(header, 0, header.Length);
        fileStream.Position = 0;
        if (read < 4) return null;
        return read < length ? header[..read] : header;
    }
}

/// <summary>Local-disk storage backend — used when no R2 configuration is present
/// (see DependencyInjection), so the app keeps working in dev without cloud credentials.</summary>
public class FileService : ImageFileServiceBase
{
    private readonly IWebHostEnvironment _env;
    private const string UploadsRoot = "uploads";

    public FileService(IWebHostEnvironment env) => _env = env;

    protected override async Task<string> StoreProcessedImageAsync(Stream processedImage, string contentType, string folder, string fileName)
    {
        var uploadPath = Path.Combine(_env.WebRootPath, UploadsRoot, folder);
        Directory.CreateDirectory(uploadPath);
        var filePath = Path.Combine(uploadPath, fileName);

        await using var fs = File.Create(filePath);
        await processedImage.CopyToAsync(fs);

        return $"/{UploadsRoot}/{folder}/{fileName}";
    }

    public override Task DeleteImageAsync(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return Task.CompletedTask;
        var filePath = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }
}

public class SlugService : ISlugService
{
    private readonly IProductRepository _productRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IOccasionRepository _occasionRepo;

    public SlugService(
        FethiyeCicekcisi.Core.Interfaces.Repositories.IProductRepository productRepo,
        FethiyeCicekcisi.Core.Interfaces.Repositories.ICategoryRepository categoryRepo,
        FethiyeCicekcisi.Core.Interfaces.Repositories.IOccasionRepository occasionRepo)
    {
        _productRepo = productRepo;
        _categoryRepo = categoryRepo;
        _occasionRepo = occasionRepo;
    }

    public string GenerateSlug(string text)
    {
        var slug = text.ToLowerInvariant()
            .Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u")
            .Replace("ş", "s").Replace("ö", "o").Replace("ç", "c")
            .Replace(" ", "-");

        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-").Trim('-');
        return slug;
    }

    public async Task<string> GenerateUniqueSlugAsync(string text, string entityType, int? excludeId = null)
    {
        var baseSlug = GenerateSlug(text);
        var slug = baseSlug;
        var counter = 1;

        while (await SlugExistsAsync(slug, entityType, excludeId))
        {
            slug = $"{baseSlug}-{counter}";
            counter++;
        }

        return slug;
    }

    private async Task<bool> SlugExistsAsync(string slug, string entityType, int? excludeId)
    {
        if (entityType == "product")
            return await _productRepo.SlugExistsAsync(slug, excludeId);
        if (entityType == "category")
        {
            var cat = await _categoryRepo.FirstOrDefaultAsync(c => c.Slug == slug);
            return cat != null && cat.Id != excludeId;
        }
        if (entityType == "occasion")
        {
            var occ = await _occasionRepo.FirstOrDefaultAsync(o => o.Slug == slug);
            return occ != null && occ.Id != excludeId;
        }
        return false;
    }
}
