using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers.TransferOwnership;

public sealed record TransferOwnershipRequest(long TargetUserId);

public sealed record TransferOwnershipCommand(long ServerId, long TargetUserId);

public sealed record TransferOwnershipResponse(
    long ServerId,
    long NewOwnerId
);

public sealed class TransferOwnershipHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<TransferOwnershipCommand, Result<TransferOwnershipResponse>>,
      IValidatable<TransferOwnershipCommand>
{
    public Error? Validate(TransferOwnershipCommand request)
    {
        if (request.TargetUserId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Target user ID is required");
        }

        return null;
    }

    public async Task<Result<TransferOwnershipResponse>> Handle(
        TransferOwnershipCommand request,
        CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var server = await dbContext.Servers
            .FirstOrDefaultAsync(s => s.Id == request.ServerId, cancellationToken);

        if (server is null)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
        }

        // Only the owner can transfer ownership
        if (server.OwnerId != userId)
        {
            return Error.Forbidden("NOT_OWNER", "Only the server owner can transfer ownership");
        }

        // Cannot transfer to yourself
        if (request.TargetUserId == userId)
        {
            return Error.Validation("SAME_USER", "Cannot transfer ownership to yourself");
        }

        // Target user must be a member of the server
        var isMember = await dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == request.TargetUserId && sm.ServerId == request.ServerId, cancellationToken);

        if (!isMember)
        {
            return Error.NotFound("TARGET_NOT_MEMBER", "Target user is not a member of this server");
        }

        server.OwnerId = request.TargetUserId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new TransferOwnershipResponse(
            ServerId: server.Id,
            NewOwnerId: server.OwnerId
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/servers/{serverId:long}/transfer-ownership", async (
            [FromRoute] long serverId,
            [FromBody] TransferOwnershipRequest request,
            [FromServices] TransferOwnershipHandler handler,
            CancellationToken ct) =>
        {
            var command = new TransferOwnershipCommand(serverId, request.TargetUserId);
            return await handler.ExecuteAsync(command, ct,
                success => Results.Ok(success));
        })
        .RequireAnyAuthorization(Policies.User)
        .WithName("TransferOwnership")
        .WithTags("Servers");
}
