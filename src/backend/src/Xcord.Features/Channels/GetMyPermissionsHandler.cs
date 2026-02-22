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

public sealed record MyChannelPermissionsDto(long Permissions);

public sealed class GetMyPermissionsHandler(
    ICurrentUserService currentUserService,
    IPermissionService permissionService)
    : IRequestHandler<GetMyPermissionsQuery, Result<MyChannelPermissionsDto>>
{
    public async Task<Result<MyChannelPermissionsDto>> Handle(
        GetMyPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var perms = await permissionService.GetChannelPermissions(userId, request.ChannelId);
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
                return await handler.ExecuteAsync(query, ct);
            })
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithTags("Channels")
            .WithName("GetMyChannelPermissions")
            .Produces<MyChannelPermissionsDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
