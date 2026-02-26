using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers;

public sealed record DeleteServerCommand(long ServerId);

public sealed class DeleteServerHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
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
