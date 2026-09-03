using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Channels;

/// <summary>
/// Returns the current user's effective permission bitfield for a specific channel.
/// Used by the frontend to gate UI actions (e.g. message compose, delete button visibility).
/// </summary>
public sealed record GetMyPermissionsQuery(long ChannelId);

/// <remarks>
/// Serialized as a string, like every other 64-bit value the API returns. It was
/// forced to a JSON number, and a permission bitfield is exactly the kind of long
/// that must not be: an owner's value is long.MaxValue, which JavaScript rounds
/// to 2^63 on parse - collapsing every permission bit to a single one and
/// denying the owner everything the UI gates on it.
/// </remarks>
public sealed record MyChannelPermissionsDto(long Permissions);

public sealed class GetMyPermissionsHandler(
    ICurrentUserService currentUserService,
    IRoleService roleService)
    : IRequestHandler<GetMyPermissionsQuery, Result<MyChannelPermissionsDto>>
{
    public async Task<Result<MyChannelPermissionsDto>> Handle(
        GetMyPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var perms = await roleService.GetChannelRoles(userId, request.ChannelId).ConfigureAwait(false);
        return new MyChannelPermissionsDto(perms);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet(
            "/api/v1/channels/{channelId}/my-permissions",
            async (
                [FromRoute] long channelId,
                IRequestHandler<GetMyPermissionsQuery, Result<MyChannelPermissionsDto>> handler,
                CancellationToken ct) =>
            {
                var query = new GetMyPermissionsQuery(channelId);
                return await handler.ExecuteAsync(query, ct).ConfigureAwait(false);
            })
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithTags("Channels")
            .WithName("GetMyChannelPermissions")
            .Produces<MyChannelPermissionsDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
