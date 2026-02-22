using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Users;

public sealed record GetUserByUsernameRequest(string Username);

public sealed record PublicUserDto(
    long Id,
    string Username,
    string DisplayName,
    string? AvatarUrl
);

public sealed class GetUserByUsernameHandler(AppDbContext dbContext)
    : IRequestHandler<GetUserByUsernameRequest, Result<PublicUserDto>>
{
    public async Task<Result<PublicUserDto>> Handle(GetUserByUsernameRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Username == request.Username)
            .Select(u => new PublicUserDto(u.Id, u.Username, u.DisplayName, u.AvatarUrl))
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return Error.NotFound("USER_NOT_FOUND", "User not found");

        return user;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/users/by-username/{username}", async (
            string username,
            [FromServices] GetUserByUsernameHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new GetUserByUsernameRequest(username), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetUserByUsername")
        .WithTags("Users");
}
