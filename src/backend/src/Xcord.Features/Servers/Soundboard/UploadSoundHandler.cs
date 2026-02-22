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

public sealed record UploadSoundCommand(long ServerId, string Name, string AudioData, int DurationMs);
public sealed record UploadSoundRequest(string Name, string AudioData, int DurationMs);

public sealed class UploadSoundHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    IHttpContextAccessor httpContextAccessor, IPermissionService permissionService)
    : IRequestHandler<UploadSoundCommand, Result<SoundResponse>>, IValidatable<UploadSoundCommand>
{
    public Error? Validate(UploadSoundCommand r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return Error.Validation("VALIDATION_ERROR", "Name is required");
        if (r.Name.Length > 80) return Error.Validation("VALIDATION_ERROR", "Name must be 80 characters or less");
        if (r.DurationMs <= 0 || r.DurationMs > 10000) return Error.Validation("VALIDATION_ERROR", "Duration must be between 1ms and 10s");
        return null;
    }

    public async Task<Result<SoundResponse>> Handle(UploadSoundCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var perm = await permissionService.EnsureServerPermission(userId, request.ServerId, Permission.ManageServer);
        if (perm.IsFailure) return Error.Forbidden("MISSING_PERMISSIONS", "You do not have permission");

        var now = DateTimeOffset.UtcNow;
        var soundId = snowflakeGenerator.NextId();
        var s3Key = $"soundboard/{request.ServerId}/{soundId}.ogg";

        var sound = new SoundboardSound
        {
            Id = soundId, ServerId = request.ServerId, Name = request.Name,
            S3Key = s3Key, AudioUrl = $"/api/v1/attachments/soundboard/{soundId}",
            DurationMs = request.DurationMs, UploadedByUserId = userId, CreatedAt = now
        };
        dbContext.SoundboardSounds.Add(sound);
        await dbContext.SaveChangesAsync(ct);

        return new SoundResponse(sound.Id, sound.Name, sound.ServerId, userId, sound.DurationMs, sound.IsDefault, sound.CreatedAt);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/servers/{serverId}/sounds", async (
            long serverId, UploadSoundRequest request,
            IRequestHandler<UploadSoundCommand, Result<SoundResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new UploadSoundCommand(serverId, request.Name, request.AudioData, request.DurationMs), ct))
        .RequireAuthorization(Policies.User)
        .WithName("UploadSound").WithTags("Soundboard");
}
