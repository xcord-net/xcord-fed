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

        await AnnounceThumbnailsAsync(
            scope,
            context,
            storageService,
            pending.Where(a => !string.IsNullOrEmpty(a.ThumbnailS3Key)).Select(a => a.Id).ToList(),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tell the conversation that a message's attachments now have thumbnails.
    /// </summary>
    /// <remarks>
    /// Thumbnailing is deliberately asynchronous, so a message is delivered
    /// before its pictures exist and renders as a download link. Nothing then
    /// announced the thumbnail, so the link stayed a link for the rest of the
    /// session and only became an image after a reload - the last step of the
    /// pipeline reached the database and stopped there.
    ///
    /// Only the attachments are sent: a message-shaped payload missing its
    /// author and body would be merged over the real one by clients.
    /// </remarks>
    private async Task AnnounceThumbnailsAsync(
        IServiceScope scope,
        AppDbContext context,
        IStorageService storageService,
        List<long> thumbnailedIds,
        CancellationToken cancellationToken)
    {
        if (thumbnailedIds.Count == 0) return;

        var notificationService = scope.ServiceProvider.GetService<INotificationService>();
        if (notificationService is null) return;

        // Re-read rather than trusting the entities loaded at the start of this
        // tick. A message sent in between links its attachments with direct SQL
        // (ExecuteUpdateAsync), which never refreshes the change tracker - so the
        // MessageId in memory is still null for exactly the attachments whose
        // thumbnail arrived after the message, which are the only ones that need
        // announcing at all.
        var linked = await context.Attachments
            .AsNoTracking()
            .Where(a => thumbnailedIds.Contains(a.Id)
                && a.MessageId != null
                && a.ThumbnailS3Key != null
                && a.ThumbnailS3Key != ""
                && a.DeletedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (linked.Count == 0) return;

        var messageIds = linked.Select(a => a.MessageId!.Value).Distinct().ToList();
        var conversationByMessage = await context.Messages
            .AsNoTracking()
            .Where(m => messageIds.Contains(m.Id))
            .Select(m => new { m.Id, m.ConversationId })
            .ToDictionaryAsync(m => m.Id, m => m.ConversationId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var group in linked.GroupBy(a => a.MessageId!.Value))
        {
            if (!conversationByMessage.TryGetValue(group.Key, out var conversationId)) continue;

            var attachments = new List<object>();
            foreach (var attachment in group)
            {
                attachments.Add(new
                {
                    id = attachment.Id,
                    fileName = attachment.FileName,
                    contentType = attachment.ContentType,
                    fileSize = attachment.FileSize,
                    width = attachment.Width,
                    height = attachment.Height,
                    downloadUrl = await storageService
                        .GenerateDownloadUrlAsync(attachment.S3Key, TimeSpan.FromHours(1))
                        .ConfigureAwait(false),
                    thumbnailUrl = await storageService
                        .GenerateDownloadUrlAsync(attachment.ThumbnailS3Key!, TimeSpan.FromHours(1))
                        .ConfigureAwait(false),
                });
            }

            try
            {
                await notificationService.NotifyConversationAsync(
                    conversationId,
                    "Chat_AttachmentsUpdated",
                    new { conversationId, messageId = group.Key, attachments },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // A picture that arrives late is better than a poller that dies.
                _logger.LogWarning(ex,
                    "Failed to announce thumbnails for message {MessageId}", group.Key);
            }
        }
    }
}
