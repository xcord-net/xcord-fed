using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers;

public sealed record GetServerQuery(long ServerId);

public sealed class GetServerHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetServerQuery, Result<ServerDto>>
{
    public async Task<Result<ServerDto>> Handle(GetServerQuery request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Check if server exists
        var server = await dbContext.Servers
            .FirstOrDefaultAsync(s => s.Id == request.ServerId, cancellationToken);

        if (server == null)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
        }

        // Check if user is a member of the server
        var isMember = await dbContext.ServerMembers
            .AnyAsync(sm => sm.UserId == userId && sm.ServerId == request.ServerId, cancellationToken);

        if (!isMember)
        {
            return Error.Forbidden("NOT_A_MEMBER", "You must be a member of this server to view it");
        }

        return new ServerDto(
            Id: server.Id,
            Name: server.Name,
            Description: server.Description,
            IconUrl: server.IconUrl,
            BannerUrl: server.BannerUrl,
            OwnerId: server.OwnerId,
            MemberCount: server.MemberCount,
            PreferredLocale: server.PreferredLocale,
            CreatedAt: server.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{id:long}", async (
            long id,
            IRequestHandler<GetServerQuery, Result<ServerDto>> handler,
            CancellationToken ct) =>
        {
            var query = new GetServerQuery(id);
            return await handler.ExecuteAsync(query, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetServer")
        .WithTags("Servers");
    }
}
