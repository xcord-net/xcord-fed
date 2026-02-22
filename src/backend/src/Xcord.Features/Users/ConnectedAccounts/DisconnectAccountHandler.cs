using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Users.ConnectedAccounts;

public sealed record DisconnectAccountCommand(long ConnectionId);
public sealed record DisconnectAccountResponse(bool Disconnected);

public sealed class DisconnectAccountHandler(
    AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<DisconnectAccountCommand, Result<DisconnectAccountResponse>>
{
    public async Task<Result<DisconnectAccountResponse>> Handle(DisconnectAccountCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var account = await dbContext.ConnectedAccounts.FirstOrDefaultAsync(c => c.Id == request.ConnectionId && c.UserId == userId, ct);
        if (account == null) return Error.NotFound("CONNECTION_NOT_FOUND", "Connection not found");

        account.DeletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return new DisconnectAccountResponse(true);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/users/@me/connections/{connectionId}", async (
            long connectionId,
            IRequestHandler<DisconnectAccountCommand, Result<DisconnectAccountResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new DisconnectAccountCommand(connectionId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("DisconnectAccount").WithTags("ConnectedAccounts");
}
