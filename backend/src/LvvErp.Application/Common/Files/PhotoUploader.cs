using FluentValidation;
using FluentValidation.Results;
using LvvErp.Application.Common.Interfaces;

namespace LvvErp.Application.Common.Files;

public static class PhotoRules
{
    public const long MaxBytes = 2 * 1024 * 1024;
}

/// <summary>Validates an uploaded photo by content (size + magic bytes) and stores it.</summary>
public sealed class PhotoUploader(IFileStorage fileStorage)
{
    /// <summary>Returns the storage path. Throws a ValidationException (field "file") for bad input.</summary>
    public async Task<string> SaveAsync(Stream content, string folder, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length == 0 || buffer.Length > PhotoRules.MaxBytes)
            throw Error($"Photo must be between 1 byte and {PhotoRules.MaxBytes / 1024 / 1024} MB.");

        var extension = ImageFormat.DetectExtension(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16)))
            ?? throw Error("Photo must be a JPG, PNG or WEBP image.");

        buffer.Position = 0;
        return await fileStorage.SaveAsync(buffer, folder, extension, ct);
    }

    /// <summary>Swaps the stored photo: deletes <paramref name="oldPath"/> after the new one is committed.</summary>
    public Task DeleteAsync(string? oldPath, CancellationToken ct = default) =>
        oldPath is null ? Task.CompletedTask : fileStorage.DeleteAsync(oldPath, ct);

    private static ValidationException Error(string message) => new([new ValidationFailure("file", message)]);
}
