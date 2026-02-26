using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Channels;

public sealed record GetChannelCommand(long ChannelId);

public sealed record GetChannelResponse(
    long Id,
    long ConversationId,
    long ServerId,
    long? CategoryId,
    string Name,
    string? Topic,
    ChannelType Type,
    int Position,
    int? SlowModeSeconds,
    bool IsNsfw,
    ForumSort? DefaultSortOrder,
    bool RequireTag,
    int? DefaultAutoArchiveDuration,
    DateTimeOffset CreatedAt
);

public sealed class GetChannelHandler(
    AppDbContext dbContext,
    IPermissionService permissionService,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetChannelCommand, Result<GetChannelResponse>>
{
    public async Task<Result<GetChannelResponse>> Handle(GetChannelCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Get channel
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel == null)
        {
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
        }

        // Check ViewChannels permission
        var permissionResult = await permissionService.EnsureChannelPermission(
            userId,
            request.ChannelId,
            Permission.ViewChannels);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        return new GetChannelResponse(
            Id: channel.Id,
            ConversationId: channel.ConversationId,
            ServerId: channel.ServerId,
            CategoryId: channel.CategoryId,
            Name: channel.Name,
            Topic: channel.Topic,
            Type: channel.Type,
            Position: channel.Position,
            SlowModeSeconds: channel.SlowModeSeconds,
            IsNsfw: channel.IsNsfw,
            DefaultSortOrder: channel.DefaultSortOrder,
            RequireTag: channel.RequireTag,
            DefaultAutoArchiveDuration: channel.DefaultAutoArchiveDuration,
            CreatedAt: channel.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/channels/{channelId}", async (
            long channelId,
            [FromServices] GetChannelHandler handler,
            CancellationToken ct) =>
        {
            var command = new GetChannelCommand(channelId);

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetChannel")
        .WithTags("Channels");
    }
}
