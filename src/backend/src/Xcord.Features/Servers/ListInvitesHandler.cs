using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Servers;

public sealed record ListInvitesQuery(long ServerId);

public sealed class ListInvitesHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<ListInvitesQuery, Result<List<InviteDto>>>
{
    public async Task<Result<List<InviteDto>>> Handle(ListInvitesQuery request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Check if server exists
        var serverExists = await dbContext.Servers
            .AnyAsync(s => s.Id == request.ServerId, cancellationToken);

        if (!serverExists)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
        }

        // Check if user is a member of the server
        var memberCheck = await dbContext.EnsureMembership(request.ServerId, userId, cancellationToken);
        if (memberCheck.IsFailure) return memberCheck.Error;

        // Get all active invites for the server
        var invites = await dbContext.Invites
            .Where(i => i.ServerId == request.ServerId)
            .Select(i => new InviteDto(
                i.Code,
                i.ServerId,
                i.CreatedByUserId,
                i.MaxUses,
                i.Uses,
                i.ExpiresAt,
                i.CreatedAt,
                i.GroupId
            ))
            .ToListAsync(cancellationToken);

        return invites;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{id:long}/invites", async (
            long id,
            IRequestHandler<ListInvitesQuery, Result<List<InviteDto>>> handler,
            CancellationToken ct) =>
        {
            var query = new ListInvitesQuery(id);
            return await handler.ExecuteAsync(query, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListInvites")
        .WithTags("Servers", "Invites");
    }
}
