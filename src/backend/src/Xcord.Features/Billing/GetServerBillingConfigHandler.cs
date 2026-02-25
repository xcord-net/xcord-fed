using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Billing;

public sealed record GetServerBillingConfigQuery(long ServerId);

public sealed class GetServerBillingConfigHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<GetServerBillingConfigQuery, Result<ServerBillingConfigDto>>
{
    public async Task<Result<ServerBillingConfigDto>> Handle(
        GetServerBillingConfigQuery request, CancellationToken cancellationToken)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var server = await dbContext.Servers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.ServerId, cancellationToken);

        if (server == null)
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");

        if (server.OwnerId != userId)
            return Error.Forbidden("NOT_OWNER", "Only the server owner can view billing configuration");

        var config = await dbContext.ServerBillingConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServerId == request.ServerId, cancellationToken);

        return new ServerBillingConfigDto(
            ServerId: request.ServerId.ToString(),
            StripeConnectedAccountId: config?.StripeConnectedAccountId,
            RevenueSharePercent: config?.RevenueSharePercent ?? 70,
            PayoutEnabled: config?.PayoutEnabled ?? false,
            IsConnected: config?.StripeConnectedAccountId != null
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/billing/dashboard", async (
            [FromRoute] long serverId,
            IRequestHandler<GetServerBillingConfigQuery, Result<ServerBillingConfigDto>> handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new GetServerBillingConfigQuery(serverId), ct);
        })
        .RequireAuthorization(Policies.User)
        .WithTags("Billing")
        .WithName("GetServerBillingConfig")
        .Produces<ServerBillingConfigDto>(200);
    }
}
