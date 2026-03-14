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

namespace Xcord.Features.Threads;

public sealed record UpdateThreadRequest(
    long ChannelId,
    long ThreadId,
    string? Title,
    bool? IsArchived,
    bool? IsLocked,
    int? AutoArchiveDurationMinutes
);

public sealed record UpdateThreadResponse(
    long Id,
    long ConversationId,
    long ChannelId,
    long? ParentMessageId,
    string? Title,
    bool IsArchived,
    bool IsLocked,
    int AutoArchiveDurationMinutes,
    DateTimeOffset LastActivityAt,
    int MessageCount,
    DateTimeOffset CreatedAt
);

public sealed class UpdateThreadHandler(
    AppDbContext dbContext,
    IRoleService roleService,
    ICurrentUserService currentUserService,
    ILogger<UpdateThreadHandler> logger)
    : IRequestHandler<UpdateThreadRequest, Result<UpdateThreadResponse>>, IValidatable<UpdateThreadRequest>
{
    private static readonly int[] ValidDurations = { 60, 1440, 4320, 10080 };

    public Error? Validate(UpdateThreadRequest request)
    {
        // Title length validation (if provided)
        if (!string.IsNullOrEmpty(request.Title) && (request.Title.Length < 1 || request.Title.Length > 100))
            return Error.Validation("VALIDATION_FAILED", "Title must be between 1 and 100 characters");

        // AutoArchiveDurationMinutes validation (if provided)
        if (request.AutoArchiveDurationMinutes.HasValue && !ValidDurations.Contains(request.AutoArchiveDurationMinutes.Value))
            return Error.Validation("VALIDATION_FAILED", $"AutoArchiveDurationMinutes must be one of: {string.Join(", ", ValidDurations)}");

        return null;
    }

    public async Task<Result<UpdateThreadResponse>> Handle(UpdateThreadRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Get thread
        var thread = await dbContext.Threads
            .FirstOrDefaultAsync(t => t.Id == request.ThreadId && t.ChannelId == request.ChannelId, cancellationToken);

        if (thread == null)
        {
            return Error.NotFound("THREAD_NOT_FOUND", "Thread not found");
        }

        // Get channel to verify server membership
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel == null)
        {
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
        }

        // Verify user is a member of the server
        var isMember = await dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == userId && sm.ServerId == channel.ServerId, cancellationToken);

        if (!isMember)
        {
            return Error.Forbidden("NOT_MEMBER", "User is not a member of this server");
        }

        // Check ManageMessages permission (used for managing threads)
        var permissionResult = await roleService.EnsureChannelRole(
            userId,
            channel.Id,
            Role.ManageMessages);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // Update fields if provided
        if (request.Title != null)
        {
            thread.Title = request.Title;
        }

        if (request.IsArchived.HasValue)
        {
            thread.IsArchived = request.IsArchived.Value;
        }

        if (request.IsLocked.HasValue)
        {
            thread.IsLocked = request.IsLocked.Value;
        }

        if (request.AutoArchiveDurationMinutes.HasValue)
        {
            thread.AutoArchiveDurationMinutes = request.AutoArchiveDurationMinutes.Value;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} updated thread {ThreadId}",
            userId, request.ThreadId);

        return new UpdateThreadResponse(
            Id: thread.Id,
            ConversationId: thread.ConversationId,
            ChannelId: thread.ChannelId,
            ParentMessageId: thread.ParentMessageId,
            Title: thread.Title,
            IsArchived: thread.IsArchived,
            IsLocked: thread.IsLocked,
            AutoArchiveDurationMinutes: thread.AutoArchiveDurationMinutes,
            LastActivityAt: thread.LastActivityAt,
            MessageCount: thread.MessageCount,
            CreatedAt: thread.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPatch("/api/v1/channels/{channelId}/threads/{threadId}", async (
            long channelId,
            long threadId,
            [FromBody] UpdateThreadBodyRequest bodyRequest,
            [FromServices] UpdateThreadHandler handler,
            CancellationToken ct) =>
        {
            var request = new UpdateThreadRequest(
                ChannelId: channelId,
                ThreadId: threadId,
                Title: bodyRequest.Title,
                IsArchived: bodyRequest.IsArchived,
                IsLocked: bodyRequest.IsLocked,
                AutoArchiveDurationMinutes: bodyRequest.AutoArchiveDurationMinutes
            );

            return await handler.ExecuteAsync(request, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UpdateThread")
        .WithTags("Threads");
    }
}

public sealed record UpdateThreadBodyRequest(
    string? Title = null,
    bool? IsArchived = null,
    bool? IsLocked = null,
    int? AutoArchiveDurationMinutes = null
);
