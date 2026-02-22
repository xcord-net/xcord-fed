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
    long? Before = null
);

public sealed class ListFriendsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<ListFriendsRequest, Result<List<FriendshipDto>>>
{
    public async Task<Result<List<FriendshipDto>>> Handle(ListFriendsRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var limit = Math.Clamp(request.Limit, 1, 200);

        // Build query for friendships (both sent and received)
        var query = dbContext.Friendships
            .Include(f => f.Sender)
            .Include(f => f.Receiver)
            .Where(f =>
                (f.SenderId == userId || f.ReceiverId == userId) &&
                f.Status == request.Status);

        if (request.Before.HasValue)
        {
            query = query.Where(f => f.Id < request.Before.Value);
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

        return dtos;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/users/@me/friends", async (
            [FromServices] ListFriendsHandler handler,
            FriendshipStatus? status,
            int? limit,
            long? before,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(
                new ListFriendsRequest(
                    Status: status ?? FriendshipStatus.Accepted,
                    Limit: limit ?? 100,
                    Before: before),
                ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListFriends")
        .WithTags("Friends");
}
