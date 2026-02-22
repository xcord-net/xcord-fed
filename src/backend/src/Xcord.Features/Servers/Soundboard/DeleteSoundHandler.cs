using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers.Soundboard;

public sealed record DeleteSoundCommand(long ServerId, long SoundId);
public sealed record DeleteSoundResponse(bool Deleted);

public sealed class DeleteSoundHandler(
    AppDbContext dbContext, IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService)
    : IRequestHandler<DeleteSoundCommand, Result<DeleteSoundResponse>>
{
    public async Task<Result<DeleteSoundResponse>> Handle(DeleteSoundCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var perm = await permissionService.EnsureServerPermission(userId, request.ServerId, Permission.ManageServer);
        if (perm.IsFailure) return Error.Forbidden("MISSING_PERMISSIONS", "You do not have permission");

        var sound = await dbContext.SoundboardSounds.FirstOrDefaultAsync(s => s.Id == request.SoundId && s.ServerId == request.ServerId, ct);
        if (sound == null) return Error.NotFound("SOUND_NOT_FOUND", "Sound not found");

        sound.DeletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return new DeleteSoundResponse(true);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/servers/{serverId}/sounds/{soundId}", async (
            long serverId, long soundId,
            IRequestHandler<DeleteSoundCommand, Result<DeleteSoundResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new DeleteSoundCommand(serverId, soundId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("DeleteSound").WithTags("Soundboard");
}
