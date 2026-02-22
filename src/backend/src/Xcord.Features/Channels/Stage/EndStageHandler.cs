using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Channels.Stage;

public sealed record EndStageCommand(long ChannelId);
public sealed record EndStageResponse(bool Ended);

public sealed class EndStageHandler(
    AppDbContext dbContext, IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService)
    : IRequestHandler<EndStageCommand, Result<EndStageResponse>>
{
    public async Task<Result<EndStageResponse>> Handle(EndStageCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var perm = await permissionService.EnsureChannelPermission(userId, request.ChannelId, Permission.ManageChannels);
        if (perm.IsFailure) return Error.Forbidden("MISSING_PERMISSIONS", "You do not have permission");

        var session = await dbContext.StageSessions.FirstOrDefaultAsync(s => s.ChannelId == request.ChannelId && s.EndedAt == null, ct);
        if (session == null) return Error.NotFound("NO_ACTIVE_STAGE", "No active stage session");

        session.EndedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return new EndStageResponse(true);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/channels/{channelId}/stage", async (
            long channelId,
            IRequestHandler<EndStageCommand, Result<EndStageResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new EndStageCommand(channelId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("EndStage").WithTags("Stage");
}
