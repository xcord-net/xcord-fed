using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using Xcord;

namespace Xcord.Infrastructure.Services;

public sealed record ImageValidationResult(byte[] Bytes, string ContentType);

public interface IImageValidator
{
    bool IsImageContentType(string contentType);

    Task<Result<ImageValidationResult>> ValidateAndReencodeAsync(
        byte[] inputBytes,
        string declaredContentType,
        CancellationToken ct);
}

public sealed class ImageSharpImageValidator : IImageValidator
{
    private static readonly HashSet<string> SupportedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
    };

    private readonly ILogger<ImageSharpImageValidator> _logger;

    public ImageSharpImageValidator(ILogger<ImageSharpImageValidator> logger)
    {
        _logger = logger;
    }

    public bool IsImageContentType(string contentType) =>
        SupportedContentTypes.Contains(contentType);

    public async Task<Result<ImageValidationResult>> ValidateAndReencodeAsync(
        byte[] inputBytes,
        string declaredContentType,
        CancellationToken ct)
    {
        if (!IsImageContentType(declaredContentType))
        {
            return Error.Validation("INVALID_CONTENT_TYPE", "Content type is not a supported image format");
        }

        try
        {
            using var inputStream = new MemoryStream(inputBytes);
            // Load via ImageSharp. This parses the actual bytes and rejects polyglots / mislabeled files.
            // ImageSharp does not execute SVG, scripts, or any active content; PNG/JPEG/GIF/WebP only.
            using var image = await Image.LoadAsync(inputStream, ct);

            // Detect the actual image format from the parsed image, not the declared content type.
            // This catches "PNG declared, JPEG body" style mislabeling.
            var detectedFormat = image.Metadata.DecodedImageFormat;
            if (detectedFormat is null)
            {
                return Error.Validation("INVALID_IMAGE", "Could not detect image format from file bytes");
            }

            // Re-encode to a canonical format. This drops EXIF, ICC profiles, embedded scripts in metadata,
            // and any trailing data that survived the parse.
            using var outputStream = new MemoryStream();
            string canonicalContentType;

            switch (detectedFormat)
            {
                case JpegFormat:
                    await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = 90 }, ct);
                    canonicalContentType = "image/jpeg";
                    break;
                case PngFormat:
                    await image.SaveAsPngAsync(outputStream, new PngEncoder(), ct);
                    canonicalContentType = "image/png";
                    break;
                case GifFormat:
                    await image.SaveAsGifAsync(outputStream, new GifEncoder(), ct);
                    canonicalContentType = "image/gif";
                    break;
                case WebpFormat:
                    await image.SaveAsWebpAsync(outputStream, new WebpEncoder(), ct);
                    canonicalContentType = "image/webp";
                    break;
                default:
                    return Error.Validation("INVALID_IMAGE", $"Unsupported image format: {detectedFormat.Name}");
            }

            // Re-encoded type must match the declared type (within the supported set) so we don't
            // surprise downstream code (e.g. emoji rendering paths that key on extension).
            if (!string.Equals(canonicalContentType, declaredContentType, StringComparison.OrdinalIgnoreCase))
            {
                return Error.Validation(
                    "CONTENT_TYPE_MISMATCH",
                    $"File body is {canonicalContentType} but was declared as {declaredContentType}");
            }

            _logger.LogDebug(
                "Re-encoded image upload: {Format}, {SourceBytes} -> {CanonicalBytes} bytes",
                canonicalContentType, inputBytes.Length, outputStream.Length);

            return new ImageValidationResult(outputStream.ToArray(), canonicalContentType);
        }
        catch (UnknownImageFormatException)
        {
            return Error.Validation("INVALID_IMAGE", "File bytes do not match any supported image format");
        }
        catch (InvalidImageContentException)
        {
            return Error.Validation("INVALID_IMAGE", "Image content is corrupt or unsupported");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Image validation failed for declared content type {ContentType}", declaredContentType);
            return Error.Validation("INVALID_IMAGE", "Image could not be processed");
        }
    }
}
