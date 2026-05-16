using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that periodically cleans up orphaned and soft-deleted attachments.
/// </summary>
public sealed class AttachmentCleanup(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<AttachmentCleanup> logger)
    : PollingBackgroundService(serviceScopeFactory, logger)
{
    private const int OrphanedRetentionHours = 24;
    private const int SoftDeletedRetentionDays = 7;

    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    protected override async Task ProcessAsync(CancellationToken ct)
    {
        await CleanupOrphanedAttachmentsAsync(ct).ConfigureAwait(false);
        await CleanupSoftDeletedAttachmentsAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Clean up unconfirmed attachments older than 24 hours.
    /// </summary>
    private async Task CleanupOrphanedAttachmentsAsync(CancellationToken cancellationToken)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();

        var cutoffTime = DateTimeOffset.UtcNow.AddHours(-OrphanedRetentionHours);

        // Query unconfirmed attachments older than retention period
        var orphanedAttachments = await context.Attachments
            .IgnoreQueryFilters() // Include soft-deleted
            .Where(a => !a.IsConfirmed && a.CreatedAt < cutoffTime)
            .ToListAsync(cancellationToken);

        if (orphanedAttachments.Count == 0)
        {
            return;
        }

        Logger.LogInformation("Found {Count} orphaned attachments to clean up", orphanedAttachments.Count);

        foreach (var attachment in orphanedAttachments)
        {
            try
            {
                // Delete from S3
                await storageService.DeleteAsync(attachment.S3Key).ConfigureAwait(false);

                // Hard delete from database
                context.Attachments.Remove(attachment);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to delete orphaned attachment {AttachmentId}", attachment.Id);
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.LogInformation("Cleaned up {Count} orphaned attachments", orphanedAttachments.Count);
    }

    /// <summary>
    /// Clean up soft-deleted attachments older than 7 days.
    /// </summary>
    private async Task CleanupSoftDeletedAttachmentsAsync(CancellationToken cancellationToken)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();

        var cutoffTime = DateTimeOffset.UtcNow.AddDays(-SoftDeletedRetentionDays);

        // Query soft-deleted attachments older than retention period
        var deletedAttachments = await context.Attachments
            .IgnoreQueryFilters() // Include soft-deleted
            .Where(a => a.DeletedAt != null && a.DeletedAt < cutoffTime)
            .ToListAsync(cancellationToken);

        if (deletedAttachments.Count == 0)
        {
            return;
        }

        Logger.LogInformation("Found {Count} soft-deleted attachments to clean up", deletedAttachments.Count);

        foreach (var attachment in deletedAttachments)
        {
            try
            {
                // Delete from S3
                await storageService.DeleteAsync(attachment.S3Key).ConfigureAwait(false);

                // Delete thumbnail if exists
                if (!string.IsNullOrEmpty(attachment.ThumbnailS3Key))
                {
                    await storageService.DeleteAsync(attachment.ThumbnailS3Key).ConfigureAwait(false);
                }

                // Hard delete from database
                context.Attachments.Remove(attachment);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to delete soft-deleted attachment {AttachmentId}", attachment.Id);
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.LogInformation("Cleaned up {Count} soft-deleted attachments", deletedAttachments.Count);
    }
}
