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

public sealed record ApproveSpeakerCommand(long ChannelId, long UserId);

public sealed class ApproveSpeakerHandler(
    AppDbContext dbContext, IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService)
    : IRequestHandler<ApproveSpeakerCommand, Result<StageSpeakerResponse>>
{
    public async Task<Result<StageSpeakerResponse>> Handle(ApproveSpeakerCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var moderatorId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var perm = await permissionService.EnsureChannelPermission(moderatorId, request.ChannelId, Permission.ManageChannels);
        if (perm.IsFailure) return Error.Forbidden("MISSING_PERMISSIONS", "You do not have permission");

        var session = await dbContext.StageSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ChannelId == request.ChannelId && s.EndedAt == null, ct);
        if (session == null) return Error.NotFound("NO_ACTIVE_STAGE", "No active stage session");

        var speaker = await dbContext.StageSpeakers
            .FirstOrDefaultAsync(s => s.StageSessionId == session.Id && s.UserId == request.UserId, ct);
        if (speaker == null) return Error.NotFound("SPEAKER_NOT_FOUND", "Speaker not found");

        speaker.Role = StageRole.Speaker;
        await dbContext.SaveChangesAsync(ct);

        return new StageSpeakerResponse(speaker.Id, speaker.UserId, "Speaker", speaker.RequestedAt);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/channels/{channelId}/stage/speakers/{userId}", async (
            long channelId, long userId,
            IRequestHandler<ApproveSpeakerCommand, Result<StageSpeakerResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new ApproveSpeakerCommand(channelId, userId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("ApproveSpeaker").WithTags("Stage");
}
