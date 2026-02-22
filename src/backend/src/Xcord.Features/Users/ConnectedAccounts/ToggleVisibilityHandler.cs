using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Users.ConnectedAccounts;

public sealed record ToggleVisibilityCommand(long ConnectionId, bool ShowOnProfile);
public sealed record ToggleVisibilityRequest(bool ShowOnProfile);

public sealed class ToggleVisibilityHandler(
    AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<ToggleVisibilityCommand, Result<ConnectionResponse>>
{
    public async Task<Result<ConnectionResponse>> Handle(ToggleVisibilityCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var account = await dbContext.ConnectedAccounts.FirstOrDefaultAsync(c => c.Id == request.ConnectionId && c.UserId == userId, ct);
        if (account == null) return Error.NotFound("CONNECTION_NOT_FOUND", "Connection not found");

        account.ShowOnProfile = request.ShowOnProfile;
        await dbContext.SaveChangesAsync(ct);

        return new ConnectionResponse(account.Id, account.Provider, account.ProviderUsername, account.IsVerified, account.ShowOnProfile, account.CreatedAt);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPatch("/api/v1/users/@me/connections/{connectionId}", async (
            long connectionId, ToggleVisibilityRequest request,
            IRequestHandler<ToggleVisibilityCommand, Result<ConnectionResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new ToggleVisibilityCommand(connectionId, request.ShowOnProfile), ct))
        .RequireAuthorization(Policies.User)
        .WithName("ToggleConnectionVisibility").WithTags("ConnectedAccounts");
}
