using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Servers;

public sealed record UpdateServerCommand(
    long ServerId,
    string? Name,
    string? Description,
    string? IconUrl,
    string? BannerUrl,
    string? PreferredLocale
);

public sealed class UpdateServerHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    ILogger<UpdateServerHandler> logger)
    : IRequestHandler<UpdateServerCommand, Result<ServerDto>>, IValidatable<UpdateServerCommand>
{
    public Error? Validate(UpdateServerCommand request)
    {
        if (request.Name != null && request.Name.Length > 100)
        {
            return Error.Validation("VALIDATION_ERROR", "Server name must not exceed 100 characters");
        }

        if (request.Description != null && request.Description.Length > 1024)
        {
            return Error.Validation("VALIDATION_ERROR", "Description must not exceed 1024 characters");
        }

        if (request.IconUrl != null && request.IconUrl.Length > 512)
        {
            return Error.Validation("VALIDATION_ERROR", "Icon URL must not exceed 512 characters");
        }

        if (request.BannerUrl != null && request.BannerUrl.Length > 512)
        {
            return Error.Validation("VALIDATION_ERROR", "Banner URL must not exceed 512 characters");
        }

        if (request.PreferredLocale != null && request.PreferredLocale.Length > 10)
        {
            return Error.Validation("VALIDATION_ERROR", "Preferred locale must not exceed 10 characters");
        }

        return null;
    }

    public async Task<Result<ServerDto>> Handle(UpdateServerCommand request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Check if server exists
        var server = await dbContext.Servers
            .FirstOrDefaultAsync(s => s.Id == request.ServerId, cancellationToken);

        if (server == null)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
        }

        // Only owner can update (for now - permissions come in 009)
        if (server.OwnerId != userId)
        {
            return Error.Forbidden("NOT_OWNER", "Only the server owner can update the server");
        }

        // Update fields if provided
        if (request.Name != null)
        {
            server.Name = request.Name;
        }

        if (request.Description != null)
        {
            server.Description = request.Description;
        }

        if (request.IconUrl != null)
        {
            server.IconUrl = request.IconUrl;
        }

        if (request.BannerUrl != null)
        {
            server.BannerUrl = request.BannerUrl;
        }

        if (request.PreferredLocale != null)
        {
            server.PreferredLocale = request.PreferredLocale;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} updated server {ServerId}",
            userId, server.Id);

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
        return app.MapPatch("/api/v1/servers/{id:long}", async (
            long id,
            UpdateServerRequest request,
            IRequestHandler<UpdateServerCommand, Result<ServerDto>> handler,
            CancellationToken ct) =>
        {
            var command = new UpdateServerCommand(
                ServerId: id,
                Name: request.Name,
                Description: request.Description,
                IconUrl: request.IconUrl,
                BannerUrl: request.BannerUrl,
                PreferredLocale: request.PreferredLocale
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UpdateServer")
        .WithTags("Servers");
    }
}
