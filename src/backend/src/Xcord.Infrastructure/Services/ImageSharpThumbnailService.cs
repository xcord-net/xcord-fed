using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// ImageSharp-based implementation of <see cref="IThumbnailService"/>.
/// </summary>
public sealed class ImageSharpThumbnailService : IThumbnailService
{
    private static readonly HashSet<string> SupportedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    };

    private readonly ILogger<ImageSharpThumbnailService> _logger;

    public ImageSharpThumbnailService(ILogger<ImageSharpThumbnailService> logger)
    {
        _logger = logger;
    }

    public bool IsImageContentType(string contentType) =>
        SupportedContentTypes.Contains(contentType);

    public async Task<ThumbnailResult> GenerateThumbnailAsync(
        byte[] imageBytes,
        string contentType,
        int maxWidth,
        int maxHeight)
    {
        using var inputStream = new MemoryStream(imageBytes);

        // Load image - ImageSharp handles JPEG, PNG, GIF (first frame), and WebP transparently.
        using var image = await Image.LoadAsync(inputStream).ConfigureAwait(false);

        // Compute thumbnail dimensions preserving aspect ratio.
        var (thumbWidth, thumbHeight) = ComputeThumbnailSize(image.Width, image.Height, maxWidth, maxHeight);

        // Only resize if the image is larger than the target dimensions.
        if (thumbWidth < image.Width || thumbHeight < image.Height)
        {
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(thumbWidth, thumbHeight),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            }));
        }
        else
        {
            // Image is already within bounds; use original dimensions.
            thumbWidth = image.Width;
            thumbHeight = image.Height;
        }

        // Encode as JPEG for efficient storage regardless of source format.
        using var outputStream = new MemoryStream();
        var encoder = new JpegEncoder { Quality = 85 };
        await image.SaveAsJpegAsync(outputStream, encoder).ConfigureAwait(false);

        _logger.LogDebug(
            "Generated thumbnail: {Width}x{Height} from source {SourceWidth}x{SourceHeight}",
            thumbWidth, thumbHeight, image.Width, image.Height);

        return new ThumbnailResult(outputStream.ToArray(), thumbWidth, thumbHeight);
    }

    private static (int Width, int Height) ComputeThumbnailSize(int srcWidth, int srcHeight, int maxWidth, int maxHeight)
    {
        if (srcWidth <= maxWidth && srcHeight <= maxHeight)
        {
            return (srcWidth, srcHeight);
        }

        var widthRatio = (double)maxWidth / srcWidth;
        var heightRatio = (double)maxHeight / srcHeight;
        var ratio = Math.Min(widthRatio, heightRatio);

        return (Math.Max(1, (int)(srcWidth * ratio)), Math.Max(1, (int)(srcHeight * ratio)));
    }
}
