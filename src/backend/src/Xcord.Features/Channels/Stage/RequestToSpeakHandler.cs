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

public sealed record RequestToSpeakCommand(long ChannelId);
public sealed record StageSpeakerResponse(long Id, long UserId, string Role, DateTimeOffset? RequestedAt);

public sealed class RequestToSpeakHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<RequestToSpeakCommand, Result<StageSpeakerResponse>>
{
    public async Task<Result<StageSpeakerResponse>> Handle(RequestToSpeakCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var session = await dbContext.StageSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ChannelId == request.ChannelId && s.EndedAt == null, ct);
        if (session == null) return Error.NotFound("NO_ACTIVE_STAGE", "No active stage session");

        var existing = await dbContext.StageSpeakers.FirstOrDefaultAsync(
            s => s.StageSessionId == session.Id && s.UserId == userId, ct);
        if (existing != null) return new StageSpeakerResponse(existing.Id, userId, existing.Role.ToString(), existing.RequestedAt);

        var now = DateTimeOffset.UtcNow;
        var speaker = new StageSpeaker
        {
            Id = snowflakeGenerator.NextId(), StageSessionId = session.Id,
            UserId = userId, Role = StageRole.Audience, RequestedAt = now, CreatedAt = now
        };
        dbContext.StageSpeakers.Add(speaker);
        await dbContext.SaveChangesAsync(ct);

        return new StageSpeakerResponse(speaker.Id, userId, "Audience", now);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/channels/{channelId}/stage/speak-requests", async (
            long channelId,
            IRequestHandler<RequestToSpeakCommand, Result<StageSpeakerResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new RequestToSpeakCommand(channelId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("RequestToSpeak").WithTags("Stage");
}
