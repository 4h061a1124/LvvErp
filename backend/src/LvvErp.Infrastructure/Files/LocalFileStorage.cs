using System.Text.RegularExpressions;
using LvvErp.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LvvErp.Infrastructure.Files;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Absolute, or relative to the API's content root.</summary>
    public string LocalRootPath { get; set; } = "uploads";

    /// <summary>URL prefix the API serves stored files under.</summary>
    public string PublicBasePath { get; set; } = "/files";
}

/// <summary>
/// Stores files on the local disk. File names are random UUIDs, so URLs can't be guessed.
/// </summary>
public sealed partial class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    private readonly string _publicBasePath;

    public LocalFileStorage(IOptions<StorageOptions> options, IHostEnvironment environment)
    {
        _root = ResolveRoot(options.Value, environment);
        _publicBasePath = options.Value.PublicBasePath.TrimEnd('/');
        Directory.CreateDirectory(_root);
    }

    public static string ResolveRoot(StorageOptions options, IHostEnvironment environment) =>
        Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.LocalRootPath));

    public async Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct = default)
    {
        if (!SafeSegment().IsMatch(folder)) throw new ArgumentException("Invalid folder name.", nameof(folder));
        if (!SafeExtension().IsMatch(extension)) throw new ArgumentException("Invalid extension.", nameof(extension));

        var relativePath = $"{folder}/{Guid.CreateVersion7():N}{extension.ToLowerInvariant()}";
        var fullPath = ToFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(file, ct);
        return relativePath;
    }

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var fullPath = ToFullPath(path);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string path) => $"{_publicBasePath}/{path}";

    private string ToFullPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));
        // Path traversal guard: never touch anything outside the storage root.
        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Path escapes the storage root.", nameof(relativePath));
        return fullPath;
    }

    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex SafeSegment();

    [GeneratedRegex(@"^\.[A-Za-z0-9]{1,8}$")]
    private static partial Regex SafeExtension();
}
