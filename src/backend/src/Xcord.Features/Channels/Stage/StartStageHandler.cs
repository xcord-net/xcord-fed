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

public sealed record StartStageCommand(long ChannelId, string? Topic);
public sealed record StartStageRequest(string? Topic);
public sealed record StageSessionResponse(long Id, long ChannelId, string? Topic, DateTimeOffset StartedAt, DateTimeOffset? EndedAt);

public sealed class StartStageHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    IHttpContextAccessor httpContextAccessor, IPermissionService permissionService)
    : IRequestHandler<StartStageCommand, Result<StageSessionResponse>>
{
    public async Task<Result<StageSessionResponse>> Handle(StartStageCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var channel = await dbContext.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct);
        if (channel == null) return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
        if (channel.Type != ChannelType.Stage) return Error.Validation("INVALID_CHANNEL_TYPE", "Channel must be a Stage channel");

        var perm = await permissionService.EnsureChannelPermission(userId, request.ChannelId, Permission.ManageChannels);
        if (perm.IsFailure) return Error.Forbidden("MISSING_PERMISSIONS", "You do not have permission to manage this channel");

        var activeSession = await dbContext.StageSessions.AsNoTracking()
            .AnyAsync(s => s.ChannelId == request.ChannelId && s.EndedAt == null, ct);
        if (activeSession) return Error.Conflict("STAGE_ALREADY_ACTIVE", "A stage session is already active in this channel");

        var now = DateTimeOffset.UtcNow;
        var session = new StageSession
        {
            Id = snowflakeGenerator.NextId(), ChannelId = request.ChannelId,
            Topic = request.Topic, StartedAt = now
        };
        dbContext.StageSessions.Add(session);

        var speaker = new StageSpeaker
        {
            Id = snowflakeGenerator.NextId(), StageSessionId = session.Id,
            UserId = userId, Role = StageRole.Speaker, CreatedAt = now
        };
        dbContext.StageSpeakers.Add(speaker);
        await dbContext.SaveChangesAsync(ct);

        return new StageSessionResponse(session.Id, session.ChannelId, session.Topic, session.StartedAt, session.EndedAt);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/channels/{channelId}/stage", async (
            long channelId, StartStageRequest request,
            IRequestHandler<StartStageCommand, Result<StageSessionResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new StartStageCommand(channelId, request.Topic), ct))
        .RequireAuthorization(Policies.User)
        .WithName("StartStage").WithTags("Stage");
}
