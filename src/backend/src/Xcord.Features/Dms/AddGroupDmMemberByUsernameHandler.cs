using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Dms;

public sealed record AddGroupDmMemberByUsernameRequest(
    long DmChannelId,
    string Username
);

public sealed class AddGroupDmMemberByUsernameHandler(
    AppDbContext dbContext,
    IRequestHandler<AddGroupDmMemberRequest, Result<bool>> addMemberHandler)
    : IRequestHandler<AddGroupDmMemberByUsernameRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(AddGroupDmMemberByUsernameRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return Error.Validation("USERNAME_REQUIRED", "Username is required");

        // Look up user by username
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (user == null)
            return Error.NotFound("USER_NOT_FOUND", "User not found");

        // Delegate to the ID-based handler
        return await addMemberHandler.Handle(
            new AddGroupDmMemberRequest(request.DmChannelId, user.Id),
            cancellationToken);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/users/@me/dms/{dmChannelId:long}/members", async (
            long dmChannelId,
            [FromBody] AddGroupDmMemberByUsernameRequest request,
            [FromServices] AddGroupDmMemberByUsernameHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(
                new AddGroupDmMemberByUsernameRequest(dmChannelId, request.Username), ct,
                onSuccess: _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("AddGroupDmMemberByUsername")
        .WithTags("DMs");
}
