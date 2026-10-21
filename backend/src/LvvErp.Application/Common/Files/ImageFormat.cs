namespace LvvErp.Application.Common.Files;

/// <summary>Detects image type from file content (magic bytes), never trusting the file name or Content-Type.</summary>
public static class ImageFormat
{
    /// <summary>Returns ".jpg", ".png" or ".webp", or null when the content is not one of those.</summary>
    public static string? DetectExtension(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
            return ".jpg";
        if (header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return ".png";
        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
            return ".webp";
        return null;
    }
}
