using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers;

public sealed record DeleteInviteCommand(long ServerId, string Code);

public sealed class DeleteInviteHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService,
    ILogger<DeleteInviteHandler> logger)
    : IRequestHandler<DeleteInviteCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteInviteCommand request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Check CreateInvite permission (controls invite management)
        var permResult = await permissionService.EnsureServerPermission(userId, request.ServerId, Permission.CreateInvite);
        if (permResult.IsFailure)
        {
            return Error.Forbidden("MISSING_PERMISSION", "You do not have permission to manage invites");
        }

        // Find the invite
        var invite = await dbContext.Invites
            .FirstOrDefaultAsync(i => i.Code == request.Code && i.ServerId == request.ServerId, cancellationToken);

        if (invite == null)
        {
            return Error.NotFound("INVITE_NOT_FOUND", "Invite not found");
        }

        // Soft delete
        invite.DeletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} revoked invite {Code} for server {ServerId}",
            userId, request.Code, request.ServerId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/invites/{code}", async (
            [FromRoute] long serverId,
            [FromRoute] string code,
            IRequestHandler<DeleteInviteCommand, Result<bool>> handler,
            CancellationToken ct) =>
        {
            var command = new DeleteInviteCommand(serverId, code);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteInvite")
        .WithTags("Servers", "Invites")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
