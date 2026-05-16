using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Xcord.Shared.Extensions;

namespace Xcord.Features.Servers;

public sealed record DeleteServerCommand(long ServerId);

public sealed class DeleteServerHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DeleteServerHandler> logger)
    : IRequestHandler<DeleteServerCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteServerCommand request, CancellationToken cancellationToken)
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

        // Owner can delete; instance admin can delete any server (used for cleanup
        // of abandoned servers from prior test runs and operator-driven moderation).
        var isAdmin = httpContextAccessor.HttpContext?.User.HasClaim(c => c.Type == "admin" && c.Value == "true") ?? false;
        if (server.OwnerId != userId && !isAdmin)
        {
            return Error.Forbidden("NOT_OWNER", "Only the server owner or instance admin can delete the server");
        }

        // Soft delete
        server.SoftDelete();

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent()).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteServer")
        .WithTags("Servers");
    }
}
