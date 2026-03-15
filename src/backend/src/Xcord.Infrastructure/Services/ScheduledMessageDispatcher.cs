using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Shared.Extensions;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that polls for scheduled messages that are due for delivery
/// and dispatches them through the normal message creation pipeline.
/// Runs every 30 seconds, processing messages in batches of 50.
/// </summary>
public sealed class ScheduledMessageDispatcher(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ScheduledMessageDispatcher> logger)
    : PollingBackgroundService(serviceScopeFactory, logger)
{
    private const int BatchSize = 50;

    protected override TimeSpan Interval => TimeSpan.FromSeconds(30);

    protected override async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var snowflakeGenerator = scope.ServiceProvider.GetRequiredService<SnowflakeIdGenerator>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();

        var now = DateTimeOffset.UtcNow;

        // Fetch a batch of due, unsent scheduled messages.
        // The global soft-delete query filter already excludes DeletedAt != null rows.
        var dueMessages = await dbContext.ScheduledMessages
            .Where(sm => sm.ScheduledAt <= now && sm.SentAt == null)
            .OrderBy(sm => sm.ScheduledAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (dueMessages.Count == 0)
            return;

        Logger.LogInformation("Dispatching {Count} scheduled messages", dueMessages.Count);

        foreach (var scheduled in dueMessages)
        {
            await DispatchOneAsync(
                dbContext, snowflakeGenerator, notificationService, roleService,
                scheduled, ct);
        }
    }

    private async Task DispatchOneAsync(
        AppDbContext dbContext,
        SnowflakeIdGenerator snowflakeGenerator,
        INotificationService notificationService,
        IRoleService roleService,
        ScheduledMessage scheduled,
        CancellationToken cancellationToken)
    {
        // Resolve the channel that owns this conversation so we can check permissions.
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConversationId == scheduled.ConversationId, cancellationToken);

        if (channel == null)
        {
            // Conversation no longer has an associated channel - soft-delete the scheduled message.
            Logger.LogWarning(
                "Scheduled message {Id}: no channel found for conversation {ConversationId}. Soft-deleting.",
                scheduled.Id, scheduled.ConversationId);
            scheduled.SoftDelete();
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // Verify the author still has SendMessages permission in the channel.
        var permissionResult = await roleService.EnsureChannelRole(
            scheduled.AuthorId, channel.Id, Role.SendMessages);

        if (permissionResult.IsFailure)
        {
            Logger.LogInformation(
                "Scheduled message {Id}: author {AuthorId} no longer has SendMessages in channel {ChannelId}. Soft-deleting.",
                scheduled.Id, scheduled.AuthorId, channel.Id);
            scheduled.SoftDelete();
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // Fetch author info for the notification payload.
        var author = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == scheduled.AuthorId, cancellationToken);

        if (author == null)
        {
            Logger.LogWarning(
                "Scheduled message {Id}: author {AuthorId} not found. Soft-deleting.",
                scheduled.Id, scheduled.AuthorId);
            scheduled.SoftDelete();
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var messageId = snowflakeGenerator.NextId();
            var messageNow = DateTimeOffset.UtcNow;

            var message = new Message
            {
                Id = messageId,
                ConversationId = scheduled.ConversationId,
                AuthorId = scheduled.AuthorId,
                Type = MessageType.Default,
                Content = scheduled.Content,
                IsPinned = false,
                CreatedAt = messageNow
            };

            dbContext.Messages.Add(message);

            // Mark the scheduled message as sent.
            scheduled.SentAt = messageNow;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            Logger.LogInformation(
                "Dispatched scheduled message {ScheduledId} as message {MessageId} in conversation {ConversationId}",
                scheduled.Id, messageId, scheduled.ConversationId);

            // Send notification after successful commit
            await notificationService.NotifyConversationAsync(
                message.ConversationId,
                "Chat_MessageCreated",
                new
                {
                    conversationId = message.ConversationId,
                    id = message.Id,
                    authorId = message.AuthorId,
                    authorUsername = author.Username,
                    authorAvatarUrl = author.AvatarUrl,
                    type = message.Type.ToString(),
                    content = message.Content,
                    metadata = (string?)null,
                    replyToId = (long?)null,
                    isPinned = message.IsPinned,
                    editedAt = (DateTimeOffset?)null,
                    createdAt = message.CreatedAt
                });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            Logger.LogError(ex,
                "Failed to dispatch scheduled message {ScheduledId}", scheduled.Id);
        }
    }
}
