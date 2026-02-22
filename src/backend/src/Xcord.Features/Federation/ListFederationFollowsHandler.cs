using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Federation;

public sealed record ListFederationFollowsRequest(long? ChannelId);

public sealed record ListFederationFollowsResponse(FederationFollowDto[] Follows);

public sealed class ListFederationFollowsHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<ListFederationFollowsRequest, Result<ListFederationFollowsResponse>>
{
    public async Task<Result<ListFederationFollowsResponse>> Handle(ListFederationFollowsRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        var query = dbContext.FederationFollows
            .AsNoTracking()
            .Where(f => f.IsActive);

        if (request.ChannelId.HasValue)
        {
            query = query.Where(f => f.LocalChannelId == request.ChannelId.Value);
        }

        var follows = await query
            .OrderByDescending(f => f.CreatedAt)
            .Take(100)
            .Select(f => new FederationFollowDto(
                f.Id,
                f.RemoteInstanceUrl,
                f.LocalChannelId,
                f.RemoteChannelId,
                f.RemoteChannelName,
                f.FollowedByUserId,
                f.IsActive,
                f.CreatedAt))
            .ToArrayAsync(cancellationToken);

        return new ListFederationFollowsResponse(follows);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/federation/follows", async (
            [FromQuery] long? channelId,
            [FromServices] ListFederationFollowsHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new ListFederationFollowsRequest(channelId), ct);
        })
        .RequireAnyAuthorization(Policies.User)
        .WithName("ListFederationFollows")
        .WithTags("Federation");
}
