using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that polls for scheduled messages that are due for delivery
/// and dispatches them through the normal message creation pipeline.
/// Runs every 30 seconds, processing messages in batches of 50.
/// </summary>
public sealed class ScheduledMessageDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledMessageDispatcher> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 50;

    public ScheduledMessageDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<ScheduledMessageDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduledMessageDispatcher background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchDueMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dispatching scheduled messages");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("ScheduledMessageDispatcher background service stopped");
    }

    private async Task DispatchDueMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var snowflakeGenerator = scope.ServiceProvider.GetRequiredService<SnowflakeIdGenerator>();
        var outboxWriter = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();
        var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();

        var now = DateTimeOffset.UtcNow;

        // Fetch a batch of due, unsent scheduled messages.
        // The global soft-delete query filter already excludes DeletedAt != null rows.
        var dueMessages = await dbContext.ScheduledMessages
            .Where(sm => sm.ScheduledAt <= now && sm.SentAt == null)
            .OrderBy(sm => sm.ScheduledAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (dueMessages.Count == 0)
            return;

        _logger.LogInformation("Dispatching {Count} scheduled messages", dueMessages.Count);

        foreach (var scheduled in dueMessages)
        {
            await DispatchOneAsync(
                dbContext, snowflakeGenerator, outboxWriter, permissionService,
                scheduled, cancellationToken);
        }
    }

    private async Task DispatchOneAsync(
        AppDbContext dbContext,
        SnowflakeIdGenerator snowflakeGenerator,
        IOutboxWriter outboxWriter,
        IPermissionService permissionService,
        ScheduledMessage scheduled,
        CancellationToken cancellationToken)
    {
        // Resolve the channel that owns this conversation so we can check permissions.
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConversationId == scheduled.ConversationId, cancellationToken);

        if (channel == null)
        {
            // Conversation no longer has an associated channel — soft-delete the scheduled message.
            _logger.LogWarning(
                "Scheduled message {Id}: no channel found for conversation {ConversationId}. Soft-deleting.",
                scheduled.Id, scheduled.ConversationId);
            scheduled.DeletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // Verify the author still has SendMessages permission in the channel.
        var permissionResult = await permissionService.EnsureChannelPermission(
            scheduled.AuthorId, channel.Id, Permission.SendMessages);

        if (permissionResult.IsFailure)
        {
            _logger.LogInformation(
                "Scheduled message {Id}: author {AuthorId} no longer has SendMessages in channel {ChannelId}. Soft-deleting.",
                scheduled.Id, scheduled.AuthorId, channel.Id);
            scheduled.DeletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // Fetch author info for the outbox event payload.
        var author = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == scheduled.AuthorId, cancellationToken);

        if (author == null)
        {
            _logger.LogWarning(
                "Scheduled message {Id}: author {AuthorId} not found. Soft-deleting.",
                scheduled.Id, scheduled.AuthorId);
            scheduled.DeletedAt = DateTimeOffset.UtcNow;
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

            // Write outbox event so clients receive the message in real time.
            await outboxWriter.WriteAsync(dbContext, "Message.Created", new
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
            }, cancellationToken);

            // Mark the scheduled message as sent.
            scheduled.SentAt = messageNow;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Dispatched scheduled message {ScheduledId} as message {MessageId} in conversation {ConversationId}",
                scheduled.Id, messageId, scheduled.ConversationId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex,
                "Failed to dispatch scheduled message {ScheduledId}", scheduled.Id);
        }
    }
}
