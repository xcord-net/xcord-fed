using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers.Soundboard;

public sealed record PlaySoundCommand(long ServerId, long SoundId);
public sealed record PlaySoundResponse(long SoundId, string Name, string AudioUrl);

public sealed class PlaySoundHandler(
    AppDbContext dbContext, IHttpContextAccessor httpContextAccessor,
    IOutboxWriter outboxWriter)
    : IRequestHandler<PlaySoundCommand, Result<PlaySoundResponse>>
{
    public async Task<Result<PlaySoundResponse>> Handle(PlaySoundCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out _))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var sound = await dbContext.SoundboardSounds.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SoundId && s.ServerId == request.ServerId, ct);
        if (sound == null) return Error.NotFound("SOUND_NOT_FOUND", "Sound not found");

        await outboxWriter.WriteAsync(dbContext, "Soundboard_Play",
            new { sound.ServerId, sound.Id, sound.AudioUrl }, ct);

        return new PlaySoundResponse(sound.Id, sound.Name, sound.AudioUrl);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/servers/{serverId}/sounds/{soundId}/play", async (
            long serverId, long soundId,
            IRequestHandler<PlaySoundCommand, Result<PlaySoundResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new PlaySoundCommand(serverId, soundId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("PlaySound").WithTags("Soundboard");
}
