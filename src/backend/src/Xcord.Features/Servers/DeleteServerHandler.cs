using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Servers;

public sealed record DeleteServerCommand(long ServerId);

public sealed class DeleteServerHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DeleteServerHandler> logger)
    : IRequestHandler<DeleteServerCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteServerCommand request, CancellationToken cancellationToken)
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

        // Only owner can delete
        if (server.OwnerId != userId)
        {
            return Error.Forbidden("NOT_OWNER", "Only the server owner can delete the server");
        }

        // Soft delete
        server.DeletedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} deleted server {ServerId}",
            userId, server.Id);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{id:long}", async (
            long id,
            IRequestHandler<DeleteServerCommand, Result<bool>> handler,
            CancellationToken ct) =>
        {
            var command = new DeleteServerCommand(id);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteServer")
        .WithTags("Servers");
    }
}
