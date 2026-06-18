using KedWear.Core.Interfaces.Repositories;
using KedWear.Core.Interfaces.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace KedWear.Application.Services;

public class FileService : IFileService
{
    private readonly IWebHostEnvironment _env;
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public FileService(IWebHostEnvironment env) => _env = env;

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName, string folder = "products")
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var uniqueName = $"{Guid.NewGuid()}{ext}";
        var uploadPath = Path.Combine(_env.WebRootPath, "images", folder);

        Directory.CreateDirectory(uploadPath);

        var filePath = Path.Combine(uploadPath, uniqueName);
        await using var stream = File.Create(filePath);
        await fileStream.CopyToAsync(stream);

        return $"/images/{folder}/{uniqueName}";
    }

    public Task DeleteImageAsync(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return Task.CompletedTask;
        var filePath = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }

    public bool IsValidImage(string fileName, long fileSize)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return AllowedExtensions.Contains(ext) && fileSize <= MaxFileSizeBytes;
    }
}

public class SlugService : ISlugService
{
    private readonly IProductRepository _productRepo;
    private readonly ICategoryRepository _categoryRepo;

    public SlugService(
        KedWear.Core.Interfaces.Repositories.IProductRepository productRepo,
        KedWear.Core.Interfaces.Repositories.ICategoryRepository categoryRepo)
    {
        _productRepo = productRepo;
        _categoryRepo = categoryRepo;
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
        {
            var product = await _productRepo.FirstOrDefaultAsync(p => p.Slug == slug);
            return product != null && product.Id != excludeId;
        }
        if (entityType == "category")
        {
            var cat = await _categoryRepo.FirstOrDefaultAsync(c => c.Slug == slug);
            return cat != null && cat.Id != excludeId;
        }
        return false;
    }
}
