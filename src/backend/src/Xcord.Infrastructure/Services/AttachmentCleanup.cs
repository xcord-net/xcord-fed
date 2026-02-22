using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that periodically cleans up orphaned and soft-deleted attachments.
/// </summary>
public sealed class AttachmentCleanup : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<AttachmentCleanup> _logger;
    private const int CleanupIntervalHours = 1;
    private const int OrphanedRetentionHours = 24;
    private const int SoftDeletedRetentionDays = 7;

    public AttachmentCleanup(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<AttachmentCleanup> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "AttachmentCleanup starting with cleanup interval {CleanupIntervalHours} hours",
            CleanupIntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOrphanedAttachmentsAsync(stoppingToken);
                await CleanupSoftDeletedAttachmentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up attachments");
            }

            await Task.Delay(TimeSpan.FromHours(CleanupIntervalHours), stoppingToken);
        }

        _logger.LogInformation("AttachmentCleanup stopping");
    }

    /// <summary>
    /// Clean up unconfirmed attachments older than 24 hours.
    /// </summary>
    private async Task CleanupOrphanedAttachmentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
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

        _logger.LogInformation("Found {Count} orphaned attachments to clean up", orphanedAttachments.Count);

        foreach (var attachment in orphanedAttachments)
        {
            try
            {
                // Delete from S3
                await storageService.DeleteAsync(attachment.S3Key);

                // Hard delete from database
                context.Attachments.Remove(attachment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete orphaned attachment {AttachmentId}", attachment.Id);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Cleaned up {Count} orphaned attachments", orphanedAttachments.Count);
    }

    /// <summary>
    /// Clean up soft-deleted attachments older than 7 days.
    /// </summary>
    private async Task CleanupSoftDeletedAttachmentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
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

        _logger.LogInformation("Found {Count} soft-deleted attachments to clean up", deletedAttachments.Count);

        foreach (var attachment in deletedAttachments)
        {
            try
            {
                // Delete from S3
                await storageService.DeleteAsync(attachment.S3Key);

                // Delete thumbnail if exists
                if (!string.IsNullOrEmpty(attachment.ThumbnailS3Key))
                {
                    await storageService.DeleteAsync(attachment.ThumbnailS3Key);
                }

                // Hard delete from database
                context.Attachments.Remove(attachment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete soft-deleted attachment {AttachmentId}", attachment.Id);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Cleaned up {Count} soft-deleted attachments", deletedAttachments.Count);
    }
}
