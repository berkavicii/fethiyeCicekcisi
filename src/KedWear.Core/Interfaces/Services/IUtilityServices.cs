using KedWear.Core.Entities;

namespace KedWear.Core.Interfaces.Services;

public interface IFileService
{
    Task<string> UploadImageAsync(Stream fileStream, string fileName, string folder = "products");
    Task DeleteImageAsync(string imageUrl);
    bool IsValidImage(string fileName, long fileSize);
}

public interface ISlugService
{
    string GenerateSlug(string text);
    Task<string> GenerateUniqueSlugAsync(string text, string entityType, int? excludeId = null);
}
