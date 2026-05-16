namespace Xcord.Infrastructure.Services;

/// <summary>
/// Recognised image container formats.
/// </summary>
public enum ImageFormat
{
    Unknown = 0,
    Png,
    Jpeg,
    Gif,
    Webp,
}

/// <summary>
/// Detects image container format from the leading magic bytes of a byte buffer.
/// Replaces inline hex literals previously scattered through <see cref="EmbedExtractor"/>.
/// See kanban #124.
/// </summary>
public static class ImageFormatDetector
{
    // PNG: 89 50 4E 47 0D 0A 1A 0A  (first 4 bytes are sufficient to disambiguate)
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    // JPEG: FF D8 FF (followed by marker e.g. E0/E1)
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    // GIF: 47 49 46 ("GIF" - both GIF87a and GIF89a)
    private static readonly byte[] GifSignature = [0x47, 0x49, 0x46];

    // WebP: RIFF????WEBP - 'R' 'I' 'F' 'F' at offset 0, 'W' 'E' 'B' 'P' at offset 8
    private static readonly byte[] WebpRiff = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] WebpTag = [0x57, 0x45, 0x42, 0x50];

    /// <summary>
    /// Inspect <paramref name="bytes"/> and return the detected format, or
    /// <see cref="ImageFormat.Unknown"/> if no signature matches.
    /// </summary>
    public static ImageFormat Detect(ReadOnlySpan<byte> bytes)
    {
        if (StartsWith(bytes, PngSignature))
        {
            return ImageFormat.Png;
        }

        if (StartsWith(bytes, JpegSignature))
        {
            return ImageFormat.Jpeg;
        }

        if (StartsWith(bytes, GifSignature))
        {
            return ImageFormat.Gif;
        }

        if (bytes.Length >= 12 &&
            StartsWith(bytes, WebpRiff) &&
            bytes.Slice(8, 4).SequenceEqual(WebpTag))
        {
            return ImageFormat.Webp;
        }

        return ImageFormat.Unknown;
    }

    /// <summary>
    /// File extension (with leading dot) for a format, or <c>null</c> if unknown.
    /// </summary>
    public static string? GetExtension(ImageFormat format) => format switch
    {
        ImageFormat.Png => ".png",
        ImageFormat.Jpeg => ".jpg",
        ImageFormat.Gif => ".gif",
        ImageFormat.Webp => ".webp",
        _ => null,
    };

    /// <summary>
    /// MIME content type for a format. Returns
    /// <c>application/octet-stream</c> for <see cref="ImageFormat.Unknown"/>.
    /// </summary>
    public static string GetContentType(ImageFormat format) => format switch
    {
        ImageFormat.Png => "image/png",
        ImageFormat.Jpeg => "image/jpeg",
        ImageFormat.Gif => "image/gif",
        ImageFormat.Webp => "image/webp",
        _ => "application/octet-stream",
    };

    private static bool StartsWith(ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> prefix)
    {
        return buffer.Length >= prefix.Length && buffer[..prefix.Length].SequenceEqual(prefix);
    }
}
