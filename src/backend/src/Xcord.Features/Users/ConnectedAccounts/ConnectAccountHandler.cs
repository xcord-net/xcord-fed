using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Users.ConnectedAccounts;

public sealed record ConnectAccountCommand(string Provider, string ProviderAccountId, string? ProviderUsername, string? AccessToken, string? RefreshToken);
public sealed record ConnectAccountRequest(string Provider, string ProviderAccountId, string? ProviderUsername, string? AccessToken, string? RefreshToken);

public sealed class ConnectAccountHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<ConnectAccountCommand, Result<ConnectionResponse>>, IValidatable<ConnectAccountCommand>
{
    public Error? Validate(ConnectAccountCommand r)
    {
        if (string.IsNullOrWhiteSpace(r.Provider)) return Error.Validation("VALIDATION_ERROR", "Provider is required");
        if (string.IsNullOrWhiteSpace(r.ProviderAccountId)) return Error.Validation("VALIDATION_ERROR", "Provider account ID is required");
        return null;
    }

    public async Task<Result<ConnectionResponse>> Handle(ConnectAccountCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var exists = await dbContext.ConnectedAccounts.AsNoTracking()
            .AnyAsync(c => c.UserId == userId && c.Provider == request.Provider, ct);
        if (exists) return Error.Conflict("ALREADY_CONNECTED", "This provider is already connected");

        var now = DateTimeOffset.UtcNow;
        var account = new ConnectedAccount
        {
            Id = snowflakeGenerator.NextId(), UserId = userId,
            Provider = request.Provider, ProviderAccountId = request.ProviderAccountId,
            ProviderUsername = request.ProviderUsername, AccessToken = request.AccessToken,
            RefreshToken = request.RefreshToken, IsVerified = true, ShowOnProfile = true, CreatedAt = now
        };
        dbContext.ConnectedAccounts.Add(account);
        await dbContext.SaveChangesAsync(ct);

        return new ConnectionResponse(account.Id, account.Provider, account.ProviderUsername, account.IsVerified, account.ShowOnProfile, account.CreatedAt);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/users/@me/connections", async (
            ConnectAccountRequest request,
            IRequestHandler<ConnectAccountCommand, Result<ConnectionResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new ConnectAccountCommand(request.Provider, request.ProviderAccountId,
                request.ProviderUsername, request.AccessToken, request.RefreshToken), ct))
        .RequireAuthorization(Policies.User)
        .WithName("ConnectAccount").WithTags("ConnectedAccounts");
}
