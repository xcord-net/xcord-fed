using Microsoft.Extensions.Logging;
using System.Text.Json;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Xcord;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Executes deferred automod actions after message persistence.
/// </summary>
public sealed class AutomodActionExecutor : IAutomodActionExecutor
{
    private readonly AppDbContext _dbContext;
    private readonly SnowflakeIdGenerator _snowflakeGenerator;
    private readonly IOutboxWriter _outboxWriter;
    private readonly ILogger<AutomodActionExecutor> _logger;

    public AutomodActionExecutor(
        AppDbContext dbContext,
        SnowflakeIdGenerator snowflakeGenerator,
        IOutboxWriter outboxWriter,
        ILogger<AutomodActionExecutor> logger)
    {
        _dbContext = dbContext;
        _snowflakeGenerator = snowflakeGenerator;
        _outboxWriter = outboxWriter;
        _logger = logger;
    }

    public async Task ExecuteDeferredActionsAsync(
        long messageId,
        long serverId,
        long channelId,
        long authorId,
        List<AutomodDeferredAction> actions,
        CancellationToken cancellationToken = default)
    {
        foreach (var action in actions)
        {
            try
            {
                switch (action.ActionType)
                {
                    case AutomodActionType.DeleteMessage:
                        var msg = await _dbContext.Messages.FindAsync(messageId);
                        if (msg != null)
                        {
                            msg.DeletedAt = DateTimeOffset.UtcNow;
                            await _dbContext.SaveChangesAsync(cancellationToken);
                        }
                        break;

                    case AutomodActionType.TimeoutUser:
                        var durationMinutes = 5;
                        if (action.Config?.TryGetValue("timeoutDurationMinutes", out var durationObj) == true)
                        {
                            if (durationObj is JsonElement je && je.TryGetInt32(out var parsed))
                                durationMinutes = parsed;
                        }

                        var timeout = new Entities.Timeout
                        {
                            Id = _snowflakeGenerator.NextId(),
                            UserId = authorId,
                            ServerId = serverId,
                            ModeratorId = null,
                            Reason = $"Automod: {action.RuleName}",
                            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(durationMinutes),
                            CreatedAt = DateTimeOffset.UtcNow
                        };
                        _dbContext.Timeouts.Add(timeout);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        break;

                    case AutomodActionType.AlertMods:
                        await _outboxWriter.WriteAsync(_dbContext, "Automod.Alert", new
                        {
                            ServerId = serverId,
                            ChannelId = channelId,
                            MessageId = messageId,
                            AuthorId = authorId,
                            RuleId = action.RuleId,
                            RuleName = action.RuleName
                        }, cancellationToken);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to execute automod deferred action {ActionType} for rule {RuleName}",
                    action.ActionType, action.RuleName);
            }
        }
    }
}
