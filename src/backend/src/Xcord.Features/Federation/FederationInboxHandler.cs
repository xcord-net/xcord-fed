using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Federation;

public sealed record FederationInboxRequest(
    string SourceInstanceUrl,
    string SourceChannelId,
    FederationInboxMessageDto[] Messages
);

public sealed record FederationInboxResponse(int Accepted, int Rejected);

public sealed class FederationInboxHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IOutboxWriter outboxWriter,
    ILogger<FederationInboxHandler> logger)
    : IRequestHandler<FederationInboxRequest, Result<FederationInboxResponse>>, IValidatable<FederationInboxRequest>
{
    public Error? Validate(FederationInboxRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceInstanceUrl))
            return Error.Validation("VALIDATION_FAILED", "SourceInstanceUrl is required");

        if (string.IsNullOrWhiteSpace(request.SourceChannelId))
            return Error.Validation("VALIDATION_FAILED", "SourceChannelId is required");

        if (request.Messages == null || request.Messages.Length == 0)
            return Error.Validation("VALIDATION_FAILED", "At least one message is required");

        if (request.Messages.Length > 50)
            return Error.Validation("VALIDATION_FAILED", "Maximum 50 messages per inbox delivery");

        return null;
    }

    public async Task<Result<FederationInboxResponse>> Handle(FederationInboxRequest request, CancellationToken cancellationToken)
    {
        var normalizedUrl = request.SourceInstanceUrl.TrimEnd('/');

        // Find all active follows for this remote channel
        var follows = await dbContext.FederationFollows
            .AsNoTracking()
            .Where(f => f.RemoteInstanceUrl == normalizedUrl
                     && f.RemoteChannelId == request.SourceChannelId
                     && f.IsActive)
            .Include(f => f.LocalChannel)
            .ToListAsync(cancellationToken);

        if (follows.Count == 0)
        {
            return Error.NotFound("NO_FOLLOWS", "No active follows for this remote channel");
        }

        var accepted = 0;
        var rejected = 0;

        foreach (var message in request.Messages)
        {
            try
            {
                foreach (var follow in follows)
                {
                    // Check if already imported
                    var alreadyImported = await dbContext.FederationMessages
                        .AnyAsync(fm => fm.FederationFollowId == follow.Id
                                     && fm.RemoteMessageId == message.RemoteMessageId,
                            cancellationToken);

                    if (alreadyImported)
                    {
                        continue;
                    }

                    // Create local message copy
                    var localMessageId = snowflakeGenerator.NextId();
                    var localMessage = new Message
                    {
                        Id = localMessageId,
                        ConversationId = follow.LocalChannel.ConversationId,
                        AuthorId = null, // System/federated message
                        Type = MessageType.Default,
                        Content = message.Content,
                        Metadata = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            federated = true,
                            sourceInstance = normalizedUrl,
                            sourceChannelId = request.SourceChannelId,
                            remoteMessageId = message.RemoteMessageId,
                            remoteAuthorName = message.AuthorName,
                            remoteAuthorAvatarUrl = message.AuthorAvatarUrl
                        }),
                        CreatedAt = message.CreatedAt
                    };

                    dbContext.Messages.Add(localMessage);

                    // Create federation message record
                    var fedMessage = new FederationMessage
                    {
                        Id = snowflakeGenerator.NextId(),
                        FederationFollowId = follow.Id,
                        RemoteMessageId = message.RemoteMessageId,
                        LocalMessageId = localMessageId,
                        RemoteAuthorName = message.AuthorName,
                        RemoteAuthorAvatarUrl = message.AuthorAvatarUrl,
                        ReceivedAt = DateTimeOffset.UtcNow
                    };

                    dbContext.FederationMessages.Add(fedMessage);

                    // Dispatch SignalR event
                    await outboxWriter.WriteAsync(dbContext, "Chat.MessageCreated", new
                    {
                        MessageId = localMessageId,
                        ConversationId = follow.LocalChannel.ConversationId,
                        Federated = true
                    }, cancellationToken);
                }

                accepted++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to process federated message {RemoteMessageId}", message.RemoteMessageId);
                rejected++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Federation inbox from {SourceUrl}: {Accepted} accepted, {Rejected} rejected",
            normalizedUrl, accepted, rejected);

        return new FederationInboxResponse(accepted, rejected);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/federation/inbox", async (
            [FromBody] FederationInboxRequest request,
            [FromServices] FederationInboxHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(request, ct);
        })
        .WithName("FederationInbox")
        .WithTags("Federation");
}
