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

namespace Xcord.Features.Friends;

public sealed record ListFriendsRequest(
    FriendshipStatus Status = FriendshipStatus.Accepted,
    int Limit = 100,
    string? Cursor = null
);

public sealed record ListFriendsResponse(
    List<FriendshipDto> Friendships,
    string? NextCursor = null
);

public sealed class ListFriendsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    ICursorService cursorService) : IRequestHandler<ListFriendsRequest, Result<ListFriendsResponse>>
{
    public async Task<Result<ListFriendsResponse>> Handle(ListFriendsRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Decode opaque cursor (returns null when no cursor was supplied)
        var cursorResult = cursorService.Decode(request.Cursor);
        if (cursorResult.IsFailure) return cursorResult.Error;
        var beforeId = cursorResult.Value;

        var limit = Math.Clamp(request.Limit, 1, 200);

        // Build query for friendships (both sent and received)
        var query = dbContext.Friendships
            .Include(f => f.Sender)
            .Include(f => f.Receiver)
            .Where(f =>
                (f.SenderId == userId || f.ReceiverId == userId) &&
                f.Status == request.Status);

        if (beforeId.HasValue)
        {
            query = query.Where(f => f.Id < beforeId.Value);
        }

        var friendships = await query
            .OrderByDescending(f => f.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        // Map to DTOs
        var dtos = friendships.Select(f => new FriendshipDto(
            f.Id,
            f.SenderId,
            f.Sender.Username,
            f.Sender.DisplayName,
            f.Sender.AvatarUrl,
            f.ReceiverId,
            f.Receiver.Username,
            f.Receiver.DisplayName,
            f.Receiver.AvatarUrl,
            f.Status,
            f.CreatedAt
        )).ToList();

        var nextCursor = dtos.Count == limit && dtos.Count > 0
            ? cursorService.Encode(dtos[^1].Id)
            : null;

        return new ListFriendsResponse(Friendships: dtos, NextCursor: nextCursor);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/users/@me/friends", async (
            [FromServices] ListFriendsHandler handler,
            FriendshipStatus? status,
            int? limit,
            string? cursor,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(
                new ListFriendsRequest(
                    Status: status ?? FriendshipStatus.Accepted,
                    Limit: limit ?? 100,
                    Cursor: cursor),
                ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListFriends")
        .WithTags("Friends");
}
