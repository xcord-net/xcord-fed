using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers.VanityUrl;

public sealed record SetVanityUrlCommand(long ServerId, string Slug);
public sealed record SetVanityUrlRequest(string Slug);

public sealed class SetVanityUrlHandler(
    AppDbContext dbContext, IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService)
    : IRequestHandler<SetVanityUrlCommand, Result<VanityUrlResponse>>, IValidatable<SetVanityUrlCommand>
{
    public Error? Validate(SetVanityUrlCommand r)
    {
        if (string.IsNullOrWhiteSpace(r.Slug)) return Error.Validation("VALIDATION_ERROR", "Slug is required");
        if (r.Slug.Length < 3) return Error.Validation("VALIDATION_ERROR", "Slug must be at least 3 characters");
        if (r.Slug.Length > 32) return Error.Validation("VALIDATION_ERROR", "Slug must be 32 characters or less");
        if (!Regex.IsMatch(r.Slug, @"^[a-zA-Z0-9-]+$")) return Error.Validation("VALIDATION_ERROR", "Slug may only contain letters, numbers, and hyphens");
        return null;
    }

    public async Task<Result<VanityUrlResponse>> Handle(SetVanityUrlCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var perm = await permissionService.EnsureServerPermission(userId, request.ServerId, Permission.ManageServer);
        if (perm.IsFailure) return Error.Forbidden("MISSING_PERMISSIONS", "You do not have permission to manage this server");

        var slug = request.Slug.ToLowerInvariant();
        var existing = await dbContext.Servers.AsNoTracking().AnyAsync(s => s.VanitySlug == slug && s.Id != request.ServerId, ct);
        if (existing) return Error.Conflict("SLUG_TAKEN", "This vanity URL is already in use");

        var server = await dbContext.Servers.FirstOrDefaultAsync(s => s.Id == request.ServerId, ct);
        if (server == null) return Error.NotFound("SERVER_NOT_FOUND", "Server not found");

        server.VanitySlug = slug;
        await dbContext.SaveChangesAsync(ct);

        return new VanityUrlResponse(server.Id, slug, $"/invite/{slug}");
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/servers/{serverId}/vanity-url", async (
            long serverId, SetVanityUrlRequest request,
            IRequestHandler<SetVanityUrlCommand, Result<VanityUrlResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new SetVanityUrlCommand(serverId, request.Slug), ct))
        .RequireAuthorization(Policies.User)
        .WithName("SetVanityUrl").WithTags("VanityUrl");
}
