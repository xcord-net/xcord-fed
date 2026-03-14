using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.ScheduledMessages;

public sealed record CreateScheduledMessageRequest(
    long ChannelId,
    string Content,
    DateTimeOffset ScheduledAt
);

public sealed record CreateScheduledMessageResponse(
    long Id,
    long ConversationId,
    long AuthorId,
    string Content,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? SentAt
);

public sealed class CreateScheduledMessageHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IRoleService roleService,
    ICurrentUserService currentUserService,
    ILogger<CreateScheduledMessageHandler> logger)
    : IRequestHandler<CreateScheduledMessageRequest, Result<CreateScheduledMessageResponse>>,
      IValidatable<CreateScheduledMessageRequest>
{
    private static readonly TimeSpan MinLead = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxLead = TimeSpan.FromDays(30);

    public Error? Validate(CreateScheduledMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return Error.Validation("VALIDATION_FAILED", "Message content is required");

        if (request.Content.Length > 4000)
            return Error.Validation("VALIDATION_FAILED", "Message content must not exceed 4000 characters");

        var now = DateTimeOffset.UtcNow;

        if (request.ScheduledAt < now + MinLead)
            return Error.Validation("VALIDATION_FAILED", "ScheduledAt must be at least 1 minute in the future");

        if (request.ScheduledAt > now + MaxLead)
            return Error.Validation("VALIDATION_FAILED", "ScheduledAt must be no more than 30 days in the future");

        return null;
    }

    public async Task<Result<CreateScheduledMessageResponse>> Handle(
        CreateScheduledMessageRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Resolve channel and its ConversationId
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel == null)
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");

        // Verify user has SendMessages permission in the channel
        var permissionResult = await roleService.EnsureChannelRole(
            userId, channel.Id, Role.SendMessages);

        if (permissionResult.IsFailure)
            return permissionResult.Error;

        var id = snowflakeGenerator.NextId();
        var scheduledMessage = new ScheduledMessage
        {
            Id = id,
            ConversationId = channel.ConversationId,
            AuthorId = userId,
            Content = request.Content,
            ScheduledAt = request.ScheduledAt
        };

        dbContext.ScheduledMessages.Add(scheduledMessage);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} scheduled message {MessageId} in channel {ChannelId} for {ScheduledAt}",
            userId, id, request.ChannelId, request.ScheduledAt);

        return new CreateScheduledMessageResponse(
            Id: scheduledMessage.Id,
            ConversationId: scheduledMessage.ConversationId,
            AuthorId: scheduledMessage.AuthorId,
            Content: scheduledMessage.Content,
            ScheduledAt: scheduledMessage.ScheduledAt,
            SentAt: scheduledMessage.SentAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/channels/{channelId}/scheduled-messages", async (
            long channelId,
            [FromBody] CreateScheduledMessageBodyRequest bodyRequest,
            [FromServices] CreateScheduledMessageHandler handler,
            CancellationToken ct) =>
        {
            var request = new CreateScheduledMessageRequest(
                ChannelId: channelId,
                Content: bodyRequest.Content,
                ScheduledAt: bodyRequest.ScheduledAt
            );

            return await handler.ExecuteAsync(request, ct,
                success => Results.Created(
                    $"/api/v1/channels/{channelId}/scheduled-messages/{success.Id}", success));
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateScheduledMessage")
        .WithTags("ScheduledMessages");
    }
}

public sealed record CreateScheduledMessageBodyRequest(
    string Content,
    DateTimeOffset ScheduledAt
);
