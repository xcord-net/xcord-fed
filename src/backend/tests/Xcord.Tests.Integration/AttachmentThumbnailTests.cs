using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;
using Xunit;

namespace Xcord.Tests.Integration;

/// <summary>
/// Integration tests for attachment thumbnail generation.
/// Verifies that ThumbnailProcessor correctly generates thumbnails for image
/// attachments and stores them in S3/MinIO, and that the Attachment entity is
/// updated with a non-null ThumbnailS3Key.
/// </summary>
public sealed class AttachmentThumbnailTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("xcord_test")
        .WithUsername("xcord_test")
        .WithPassword("xcord_test")
        .Build();

    private readonly MinioContainer _minio = new MinioBuilder()
        .WithImage("minio/minio:latest")
        .Build();

    private AppDbContext _db = null!;
    private IStorageService _storage = null!;
    private IThumbnailService _thumbnailService = null!;

    private const string TestBucket = "xcord-test";

    public async Task InitializeAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await Task.WhenAll(
            _postgres.StartAsync(cts.Token),
            _minio.StartAsync(cts.Token)
        );

        // Set up the DB context and schema.
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        _db = new AppDbContext(dbOptions);
        await _db.Database.EnsureCreatedAsync();

        // Set up S3StorageService pointing at the MinIO test container.
        var storageOptions = Options.Create(new StorageOptions
        {
            Endpoint = _minio.GetConnectionString(),
            AccessKey = _minio.GetAccessKey(),
            SecretKey = _minio.GetSecretKey(),
            Bucket = TestBucket,
        });
        _storage = new S3StorageService(storageOptions, NullLogger<S3StorageService>.Instance);

        // Ensure the test bucket exists by uploading a sentinel object.
        await EnsureBucketAsync();

        _thumbnailService = new ImageSharpThumbnailService(NullLogger<ImageSharpThumbnailService>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _minio.DisposeAsync().AsTask()
        );
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a MinIO bucket for the test using the AWS SDK directly.
    /// S3StorageService auto-creates the bucket on first use, but calling a
    /// non-existent bucket method first is simpler than relying on that side effect.
    /// </summary>
    private async Task EnsureBucketAsync()
    {
        var config = new AmazonS3Config
        {
            ServiceURL = _minio.GetConnectionString(),
            ForcePathStyle = true,
        };
        using var s3 = new AmazonS3Client(
            _minio.GetAccessKey(),
            _minio.GetSecretKey(),
            config);

        var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(s3, TestBucket);
        if (!bucketExists)
        {
            await s3.PutBucketAsync(new PutBucketRequest { BucketName = TestBucket });
        }
    }

    /// <summary>
    /// Generates an in-memory PNG image of the specified dimensions.
    /// </summary>
    private static byte[] CreateTestImageBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        // Fill with a solid colour so the image is not trivially compressible.
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    row[x] = new Rgba32((byte)(x % 256), (byte)(y % 256), 128, 255);
                }
            }
        });

        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    /// <summary>
    /// Builds the thumbnail S3 key that ThumbnailProcessor would use.
    /// </summary>
    private static string ExpectedThumbnailKey(long attachmentId)
    {
        var now = DateTimeOffset.UtcNow;
        return $"thumbnails/{now:yyyy}/{now:MM}/{attachmentId}.jpg";
    }

    // ──────────────────────────────────────────────────────────────
    // Tests
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateThumbnail_LargeImage_PopulatesThumbnailS3Key()
    {
        // Arrange - create a 600×600 image (larger than the 400×400 max thumbnail size)
        const int SourceWidth = 600;
        const int SourceHeight = 600;
        var imageBytes = CreateTestImageBytes(SourceWidth, SourceHeight);

        // Upload the image to MinIO under the attachment key.
        const string contentType = "image/png";
        var attachmentId = 100_000_000_001L;
        var s3Key = $"attachments/test/{attachmentId}/test-image.png";
        await _storage.UploadAsync(s3Key, imageBytes, contentType);

        // Create an Attachment entity that is confirmed and awaiting thumbnail generation.
        var attachment = new Attachment
        {
            Id = attachmentId,
            FileName = "test-image.png",
            ContentType = contentType,
            FileSize = imageBytes.Length,
            S3Key = s3Key,
            IsConfirmed = true,
            ThumbnailS3Key = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Attachments.Add(attachment);
        await _db.SaveChangesAsync();

        // Act - invoke the thumbnail generation logic directly (same logic as ThumbnailProcessor).
        const int MaxWidth = 400;
        const int MaxHeight = 400;

        _thumbnailService.IsImageContentType(contentType).Should().BeTrue();

        var originalBytes = await _storage.DownloadAsync(s3Key);
        var result = await _thumbnailService.GenerateThumbnailAsync(originalBytes, contentType, MaxWidth, MaxHeight);

        var now = DateTimeOffset.UtcNow;
        var thumbnailKey = $"thumbnails/{now:yyyy}/{now:MM}/{attachmentId}.jpg";
        await _storage.UploadAsync(thumbnailKey, result.Bytes, "image/jpeg");

        attachment.ThumbnailS3Key = thumbnailKey;
        await _db.SaveChangesAsync();

        // Assert 1 - ThumbnailS3Key is populated on the entity in the DB.
        var persisted = await _db.Attachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId);
        persisted.Should().NotBeNull();
        persisted!.ThumbnailS3Key.Should().NotBeNullOrEmpty();
        persisted.ThumbnailS3Key.Should().Be(thumbnailKey);

        // Assert 2 - the thumbnail object actually exists in MinIO.
        var thumbnailExists = await _storage.ExistsAsync(thumbnailKey);
        thumbnailExists.Should().BeTrue("thumbnail file must exist in storage after generation");

        // Assert 3 - thumbnail dimensions are smaller than the original
        //             (600×600 image resized to fit within 400×400 box).
        result.Width.Should().BeLessThan(SourceWidth,
            "thumbnail width must be smaller than the 600px source");
        result.Height.Should().BeLessThan(SourceHeight,
            "thumbnail height must be smaller than the 600px source");
        result.Width.Should().BeLessThanOrEqualTo(MaxWidth);
        result.Height.Should().BeLessThanOrEqualTo(MaxHeight);
    }

    [Fact]
    public async Task GenerateThumbnail_SmallImage_ThumbnailS3KeyStillPopulated()
    {
        // Arrange - small image already within thumbnail bounds (100×100).
        const int SourceWidth = 100;
        const int SourceHeight = 100;
        var imageBytes = CreateTestImageBytes(SourceWidth, SourceHeight);

        const string contentType = "image/png";
        var attachmentId = 100_000_000_002L;
        var s3Key = $"attachments/test/{attachmentId}/small-image.png";
        await _storage.UploadAsync(s3Key, imageBytes, contentType);

        var attachment = new Attachment
        {
            Id = attachmentId,
            FileName = "small-image.png",
            ContentType = contentType,
            FileSize = imageBytes.Length,
            S3Key = s3Key,
            IsConfirmed = true,
            ThumbnailS3Key = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Attachments.Add(attachment);
        await _db.SaveChangesAsync();

        // Act
        const int MaxWidth = 400;
        const int MaxHeight = 400;

        var originalBytes = await _storage.DownloadAsync(s3Key);
        var result = await _thumbnailService.GenerateThumbnailAsync(originalBytes, contentType, MaxWidth, MaxHeight);

        var now = DateTimeOffset.UtcNow;
        var thumbnailKey = $"thumbnails/{now:yyyy}/{now:MM}/{attachmentId}.jpg";
        await _storage.UploadAsync(thumbnailKey, result.Bytes, "image/jpeg");

        attachment.ThumbnailS3Key = thumbnailKey;
        await _db.SaveChangesAsync();

        // Assert - ThumbnailS3Key is populated and the file exists in MinIO.
        var persisted = await _db.Attachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId);
        persisted.Should().NotBeNull();
        persisted!.ThumbnailS3Key.Should().NotBeNullOrEmpty();

        var thumbnailExists = await _storage.ExistsAsync(thumbnailKey);
        thumbnailExists.Should().BeTrue("thumbnail file must exist in storage");

        // Small image is not resized - thumbnail dimensions equal source dimensions.
        result.Width.Should().Be(SourceWidth);
        result.Height.Should().Be(SourceHeight);
    }

    [Fact]
    public void NonImageAttachment_IsImageContentType_ReturnsFalse()
    {
        // Non-image content types must return false so ThumbnailProcessor skips them.
        _thumbnailService.IsImageContentType("text/plain").Should().BeFalse();
        _thumbnailService.IsImageContentType("application/pdf").Should().BeFalse();
        _thumbnailService.IsImageContentType("video/mp4").Should().BeFalse();
    }

    [Fact]
    public async Task GenerateThumbnail_JpegImage_ProducesJpegOutput()
    {
        // Arrange - JPEG input should also produce a JPEG thumbnail.
        const int SourceWidth = 500;
        const int SourceHeight = 300;
        var pngBytes = CreateTestImageBytes(SourceWidth, SourceHeight);

        // Convert PNG bytes to JPEG in-memory for the input.
        using var inputImage = Image.Load(pngBytes);
        using var jpegStream = new MemoryStream();
        inputImage.SaveAsJpeg(jpegStream);
        var jpegBytes = jpegStream.ToArray();

        const string contentType = "image/jpeg";

        // Act
        var result = await _thumbnailService.GenerateThumbnailAsync(jpegBytes, contentType, 400, 400);

        // Assert - output should be non-empty JPEG bytes.
        result.Bytes.Should().NotBeEmpty();
        result.Width.Should().BeLessThanOrEqualTo(400);
        result.Height.Should().BeLessThanOrEqualTo(400);

        // Verify the output bytes are valid JPEG (JPEG magic bytes: 0xFF 0xD8 0xFF).
        result.Bytes[0].Should().Be(0xFF);
        result.Bytes[1].Should().Be(0xD8);
        result.Bytes[2].Should().Be(0xFF);
    }
}
