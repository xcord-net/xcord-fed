using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Friends;

public sealed record SendFriendRequestByUsernameRequest(string Username);

public sealed class SendFriendRequestByUsernameHandler(
    AppDbContext dbContext,
    IRequestHandler<SendFriendRequestRequest, Result<FriendshipDto>> sendFriendRequestHandler)
    : IRequestHandler<SendFriendRequestByUsernameRequest, Result<FriendshipDto>>
{
    public async Task<Result<FriendshipDto>> Handle(SendFriendRequestByUsernameRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return Error.Validation("USERNAME_REQUIRED", "Username is required");

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (user == null)
            return Error.NotFound("USER_NOT_FOUND", "User not found");

        return await sendFriendRequestHandler.Handle(new SendFriendRequestRequest(user.Id), cancellationToken);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/friends/request", async (
            [FromBody] SendFriendRequestByUsernameRequest request,
            [FromServices] SendFriendRequestByUsernameHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(request, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("SendFriendRequestByUsername")
        .WithTags("Friends");
}
