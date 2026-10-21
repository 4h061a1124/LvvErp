namespace LvvErp.Application.Common.Interfaces;

/// <summary>
/// Stores uploaded files. Paths are storage-relative with forward slashes ("employees/abc.webp")
/// so the backing store (local disk today, MinIO/S3 later) can change without touching callers.
/// </summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);

    /// <summary>URL the browser can load the file from.</summary>
    string GetPublicUrl(string path);
}
