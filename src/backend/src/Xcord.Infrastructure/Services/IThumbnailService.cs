namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for generating image thumbnails.
/// </summary>
public interface IThumbnailService
{
    /// <summary>
    /// Returns true if the given MIME content type is a supported image format.
    /// </summary>
    bool IsImageContentType(string contentType);

    /// <summary>
    /// Generate a thumbnail from the provided image bytes.
    /// The thumbnail fits within <paramref name="maxWidth"/> x <paramref name="maxHeight"/>
    /// while preserving the original aspect ratio. Images smaller than those dimensions are
    /// returned as-is (re-encoded as JPEG). GIF input uses the first frame.
    /// </summary>
    /// <param name="imageBytes">Raw image bytes (JPEG, PNG, GIF, or WebP).</param>
    /// <param name="contentType">MIME content type of the source image.</param>
    /// <param name="maxWidth">Maximum thumbnail width in pixels.</param>
    /// <param name="maxHeight">Maximum thumbnail height in pixels.</param>
    /// <returns>Thumbnail bytes encoded as JPEG, and the actual thumbnail dimensions.</returns>
    Task<ThumbnailResult> GenerateThumbnailAsync(byte[] imageBytes, string contentType, int maxWidth, int maxHeight);
}

/// <summary>
/// Result of a thumbnail generation operation.
/// </summary>
public sealed record ThumbnailResult(
    byte[] Bytes,
    int Width,
    int Height
);
