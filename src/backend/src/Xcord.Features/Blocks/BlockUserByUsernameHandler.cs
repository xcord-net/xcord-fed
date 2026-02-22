using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Blocks;

public sealed record BlockUserByUsernameRequest(string Username);

public sealed class BlockUserByUsernameHandler(
    AppDbContext dbContext,
    IRequestHandler<BlockUserRequest, Result<UserBlockDto>> blockUserHandler)
    : IRequestHandler<BlockUserByUsernameRequest, Result<UserBlockDto>>
{
    public async Task<Result<UserBlockDto>> Handle(BlockUserByUsernameRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return Error.Validation("USERNAME_REQUIRED", "Username is required");

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (user == null)
            return Error.NotFound("USER_NOT_FOUND", "User not found");

        return await blockUserHandler.Handle(new BlockUserRequest(user.Id), cancellationToken);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/users/@me/blocks", async (
            [FromBody] BlockUserByUsernameRequest request,
            [FromServices] BlockUserByUsernameHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(request, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("BlockUserByUsername")
        .WithTags("Blocks");
}
