using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Dms;

public sealed record GetDmRequest(
    long DmChannelId
);

public sealed class GetDmHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<GetDmRequest, Result<DmChannelDto>>
{
    public async Task<Result<DmChannelDto>> Handle(GetDmRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var currentUserId = userIdResult.Value;

        // Get DM channel with members
        var dmChannel = await dbContext.DmChannels
            .Include(dm => dm.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(dm => dm.Id == request.DmChannelId, cancellationToken);

        if (dmChannel == null)
        {
            return Error.NotFound("DM_NOT_FOUND", "DM channel not found");
        }

        // Verify current user is a member
        var isMember = dmChannel.Members.Any(m => m.UserId == currentUserId);
        if (!isMember)
        {
            return Error.Forbidden("NOT_MEMBER", "You are not a member of this DM channel");
        }

        // Get last message preview
        var lastMessage = await dbContext.Messages
            .Where(m => m.ConversationId == dmChannel.ConversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MessagePreviewDto(
                m.Id,
                m.AuthorId,
                m.Content ?? "",
                m.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        var members = dmChannel.Members
            .Select(m => new DmMemberDto(
                m.UserId,
                m.User.Username,
                m.User.DisplayName,
                m.User.AvatarUrl,
                m.JoinedAt))
            .ToArray();

        return new DmChannelDto(
            dmChannel.Id,
            dmChannel.ConversationId,
            dmChannel.IsGroup,
            dmChannel.Name,
            dmChannel.OwnerId,
            members,
            lastMessage
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/users/@me/dms/{dmChannelId:long}", async (
            long dmChannelId,
            [FromServices] GetDmHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new GetDmRequest(dmChannelId), ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetDm")
        .WithTags("DMs");
}
