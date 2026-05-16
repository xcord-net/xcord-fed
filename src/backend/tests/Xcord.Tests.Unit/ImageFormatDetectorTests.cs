using FluentAssertions;
using Xcord.Infrastructure.Services;

namespace Xcord.Tests.Unit;

public sealed class ImageFormatDetectorTests
{
    [Fact]
    public void Detect_RecognizesPng()
    {
        // PNG signature: 89 50 4E 47 0D 0A 1A 0A
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        ImageFormatDetector.Detect(bytes).Should().Be(ImageFormat.Png);
    }

    [Fact]
    public void Detect_RecognizesJpeg()
    {
        // JPEG/JFIF: FF D8 FF E0
        byte[] bytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

        ImageFormatDetector.Detect(bytes).Should().Be(ImageFormat.Jpeg);
    }

    [Fact]
    public void Detect_RecognizesGif()
    {
        // GIF89a header
        byte[] bytes = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61];

        ImageFormatDetector.Detect(bytes).Should().Be(ImageFormat.Gif);
    }

    [Fact]
    public void Detect_RecognizesWebp()
    {
        // RIFF????WEBP
        byte[] bytes = [
            0x52, 0x49, 0x46, 0x46,  // RIFF
            0x00, 0x00, 0x00, 0x00,  // file size (4 bytes, value irrelevant for detection)
            0x57, 0x45, 0x42, 0x50,  // WEBP
        ];

        ImageFormatDetector.Detect(bytes).Should().Be(ImageFormat.Webp);
    }

    [Fact]
    public void Detect_ReturnsUnknownForUnrelatedBytes()
    {
        byte[] bytes = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B];

        ImageFormatDetector.Detect(bytes).Should().Be(ImageFormat.Unknown);
    }

    [Fact]
    public void Detect_ReturnsUnknownForBufferShorterThanShortestSignature()
    {
        byte[] bytes = [0x89, 0x50];

        ImageFormatDetector.Detect(bytes).Should().Be(ImageFormat.Unknown);
    }

    [Fact]
    public void Detect_WebpRequiresFullTwelveByteHeader()
    {
        // Only RIFF prefix, no WEBP tag at offset 8
        byte[] bytes = [
            0x52, 0x49, 0x46, 0x46,
            0x00, 0x00, 0x00, 0x00,
            0x41, 0x42, 0x43, 0x44,
        ];

        ImageFormatDetector.Detect(bytes).Should().Be(ImageFormat.Unknown);
    }

    [Theory]
    [InlineData(ImageFormat.Png, ".png")]
    [InlineData(ImageFormat.Jpeg, ".jpg")]
    [InlineData(ImageFormat.Gif, ".gif")]
    [InlineData(ImageFormat.Webp, ".webp")]
    public void GetExtension_ReturnsExpectedExtension(ImageFormat format, string expected)
    {
        ImageFormatDetector.GetExtension(format).Should().Be(expected);
    }

    [Fact]
    public void GetExtension_ReturnsNullForUnknown()
    {
        ImageFormatDetector.GetExtension(ImageFormat.Unknown).Should().BeNull();
    }

    [Theory]
    [InlineData(ImageFormat.Png, "image/png")]
    [InlineData(ImageFormat.Jpeg, "image/jpeg")]
    [InlineData(ImageFormat.Gif, "image/gif")]
    [InlineData(ImageFormat.Webp, "image/webp")]
    [InlineData(ImageFormat.Unknown, "application/octet-stream")]
    public void GetContentType_ReturnsExpectedMime(ImageFormat format, string expected)
    {
        ImageFormatDetector.GetContentType(format).Should().Be(expected);
    }
}
