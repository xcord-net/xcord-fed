using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Users.ProfileDecorations;

public sealed record UpdateDecorationCommand(string? BannerUrl, string? BannerColor, string? AvatarFrameUrl, string? ProfileEffect, string? Bio, string? Pronouns);
public sealed record UpdateDecorationRequest(string? BannerUrl, string? BannerColor, string? AvatarFrameUrl, string? ProfileEffect, string? Bio, string? Pronouns);

public sealed class UpdateDecorationHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<UpdateDecorationCommand, Result<DecorationResponse>>, IValidatable<UpdateDecorationCommand>
{
    public Error? Validate(UpdateDecorationCommand r)
    {
        if (r.Bio != null && r.Bio.Length > 190) return Error.Validation("VALIDATION_ERROR", "Bio must be 190 characters or less");
        if (r.Pronouns != null && r.Pronouns.Length > 50) return Error.Validation("VALIDATION_ERROR", "Pronouns must be 50 characters or less");
        return null;
    }

    public async Task<Result<DecorationResponse>> Handle(UpdateDecorationCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var now = DateTimeOffset.UtcNow;
        var decoration = await dbContext.ProfileDecorations.FirstOrDefaultAsync(d => d.UserId == userId, ct);

        if (decoration == null)
        {
            decoration = new ProfileDecoration
            {
                Id = snowflakeGenerator.NextId(), UserId = userId,
                BannerUrl = request.BannerUrl, BannerColor = request.BannerColor,
                AvatarFrameUrl = request.AvatarFrameUrl, ProfileEffect = request.ProfileEffect,
                Bio = request.Bio, Pronouns = request.Pronouns, CreatedAt = now, UpdatedAt = now
            };
            dbContext.ProfileDecorations.Add(decoration);
        }
        else
        {
            decoration.BannerUrl = request.BannerUrl;
            decoration.BannerColor = request.BannerColor;
            decoration.AvatarFrameUrl = request.AvatarFrameUrl;
            decoration.ProfileEffect = request.ProfileEffect;
            decoration.Bio = request.Bio;
            decoration.Pronouns = request.Pronouns;
            decoration.UpdatedAt = now;
        }
        await dbContext.SaveChangesAsync(ct);

        return new DecorationResponse(decoration.Id, userId, decoration.BannerUrl, decoration.BannerColor,
            decoration.AvatarFrameUrl, decoration.ProfileEffect, decoration.Bio, decoration.Pronouns);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/users/@me/decorations", async (
            UpdateDecorationRequest request,
            IRequestHandler<UpdateDecorationCommand, Result<DecorationResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new UpdateDecorationCommand(request.BannerUrl, request.BannerColor,
                request.AvatarFrameUrl, request.ProfileEffect, request.Bio, request.Pronouns), ct))
        .RequireAuthorization(Policies.User)
        .WithName("UpdateDecoration").WithTags("ProfileDecorations");
}
