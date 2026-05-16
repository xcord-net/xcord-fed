using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Forums;

public sealed record ListForumTagsCommand(
    long ChannelId
);

public sealed record ListForumTagsResponse(
    List<ForumTagDto> Tags
);

public sealed record ForumTagDto(
    long Id,
    long ChannelId,
    string Name,
    string? EmojiUnicode,
    long? EmojiId,
    bool IsModerated,
    int Position
);

public sealed class ListForumTagsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<ListForumTagsCommand, Result<ListForumTagsResponse>>
{
    public async Task<Result<ListForumTagsResponse>> Handle(ListForumTagsCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel == null)
        {
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
        }

        var isMember = await dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == userId && sm.ServerId == channel.ServerId, cancellationToken);

        if (!isMember)
        {
            return Error.Forbidden("NOT_MEMBER", "User is not a member of this server");
        }

        var tags = await dbContext.ForumTags
            .AsNoTracking()
            .Where(ft => ft.ChannelId == request.ChannelId)
            .OrderBy(ft => ft.Position)
            .Select(ft => new ForumTagDto(
                ft.Id,
                ft.ChannelId,
                ft.Name,
                ft.EmojiUnicode,
                ft.EmojiId,
                ft.IsModerated,
                ft.Position
            ))
            .ToListAsync(cancellationToken);

        return new ListForumTagsResponse(tags);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/channels/{channelId}/tags", async (
            long channelId,
            IRequestHandler<ListForumTagsCommand, Result<ListForumTagsResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new ListForumTagsCommand(ChannelId: channelId);
            return await handler.ExecuteAsync(command, ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListForumTags")
        .WithTags("Forums");
    }
}
