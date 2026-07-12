using KedWear.Core.Entities;

namespace KedWear.Core.Interfaces.Services;

public interface IFileService
{
    Task<string> UploadImageAsync(Stream fileStream, string fileName, string folder = "products");

    /// <summary>Same processing pipeline as UploadImageAsync (resize to max dimension,
    /// re-encode/compress), but reads from a local file path already on disk instead of an
    /// HTTP-uploaded stream — used to import pre-existing product photos during seeding.</summary>
    Task<string> ImportLocalImageAsync(string sourceFilePath, string folder = "products");

    Task DeleteImageAsync(string imageUrl);
    bool IsValidImage(string fileName, long fileSize);

    /// <summary>Sniffs the file's magic bytes to confirm it's really an image of an allowed
    /// type, since a spoofed extension/Content-Type header can't be trusted on its own.
    /// Resets the stream position back to 0 before returning.</summary>
    bool IsValidImageContent(Stream fileStream);

    /// <summary>Returns null if the file passes extension/size checks, otherwise a
    /// human-readable (Turkish) reason — used to tell an admin exactly why a specific
    /// upload was skipped instead of a generic "might be any of these three things".</summary>
    string? GetImageValidationError(string fileName, long fileSize);

    /// <summary>True if the file's actual bytes are a HEIC/HEIF container, regardless of its
    /// extension — catches files an iOS share/export flow mislabeled (e.g. ".jpg" that's
    /// still really HEIC), which an extension-only check would miss.</summary>
    bool IsHeicContent(Stream fileStream);
}

public interface ISlugService
{
    string GenerateSlug(string text);
    Task<string> GenerateUniqueSlugAsync(string text, string entityType, int? excludeId = null);
}
