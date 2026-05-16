using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that generates thumbnails for image attachments.
/// Polls for confirmed image attachments that have no thumbnail yet, downloads the original
/// from S3, generates a max-400px thumbnail via <see cref="IThumbnailService"/>, and uploads
/// the result back to S3, storing the key in <c>ThumbnailS3Key</c>.
/// </summary>
public sealed class ThumbnailProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ThumbnailProcessor> _logger;

    private const int PollingIntervalSeconds = 2;
    private const int BatchSize = 10;
    private const int ThumbnailMaxWidth = 400;
    private const int ThumbnailMaxHeight = 400;

    public ThumbnailProcessor(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ThumbnailProcessor> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ThumbnailProcessor starting with polling interval {PollingIntervalSeconds} seconds",
            PollingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingThumbnailsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing thumbnails");
            }

            await Task.Delay(TimeSpan.FromSeconds(PollingIntervalSeconds), stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("ThumbnailProcessor stopping");
    }

    private async Task ProcessPendingThumbnailsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var thumbnailService = scope.ServiceProvider.GetRequiredService<IThumbnailService>();

        // Find confirmed image attachments that still need a thumbnail.
        // ThumbnailS3Key == null means no thumbnail has been generated yet.
        var pending = await context.Attachments
            .Where(a => a.IsConfirmed
                && a.ThumbnailS3Key == null
                && a.DeletedAt == null)
            .OrderBy(a => a.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Processing {Count} attachments for thumbnail generation", pending.Count);

        foreach (var attachment in pending)
        {
            // Skip non-image content types - set a sentinel to avoid re-querying them.
            if (!thumbnailService.IsImageContentType(attachment.ContentType))
            {
                // Use an empty string as a sentinel: "not applicable, skip forever".
                attachment.ThumbnailS3Key = string.Empty;
                continue;
            }

            try
            {
                // Download original image from S3.
                var imageBytes = await storageService.DownloadAsync(attachment.S3Key).ConfigureAwait(false);

                // Generate thumbnail.
                var result = await thumbnailService.GenerateThumbnailAsync(
                    imageBytes,
                    attachment.ContentType,
                    ThumbnailMaxWidth,
                    ThumbnailMaxHeight);

                // Store thumbnail under: thumbnails/{yyyy}/{MM}/{attachmentId}.jpg
                var now = DateTimeOffset.UtcNow;
                var thumbnailKey = $"thumbnails/{now:yyyy}/{now:MM}/{attachment.Id}.jpg";

                await storageService.UploadAsync(thumbnailKey, result.Bytes, "image/jpeg").ConfigureAwait(false);

                attachment.ThumbnailS3Key = thumbnailKey;

                _logger.LogInformation(
                    "Generated thumbnail for attachment {AttachmentId}: {Key} ({Width}x{Height})",
                    attachment.Id, thumbnailKey, result.Width, result.Height);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate thumbnail for attachment {AttachmentId}", attachment.Id);

                // Mark as processed with sentinel to avoid an infinite retry loop.
                attachment.ThumbnailS3Key = string.Empty;
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
