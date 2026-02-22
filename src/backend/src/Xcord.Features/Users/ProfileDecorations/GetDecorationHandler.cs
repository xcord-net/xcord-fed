using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Users.ProfileDecorations;

public sealed record GetDecorationQuery(long UserId);
public sealed record DecorationResponse(long Id, long UserId, string? BannerUrl, string? BannerColor, string? AvatarFrameUrl, string? ProfileEffect, string? Bio, string? Pronouns);

public sealed class GetDecorationHandler(AppDbContext dbContext)
    : IRequestHandler<GetDecorationQuery, Result<DecorationResponse>>
{
    public async Task<Result<DecorationResponse>> Handle(GetDecorationQuery request, CancellationToken ct)
    {
        var decoration = await dbContext.ProfileDecorations.AsNoTracking()
            .Where(d => d.UserId == request.UserId)
            .Select(d => new DecorationResponse(d.Id, d.UserId, d.BannerUrl, d.BannerColor, d.AvatarFrameUrl, d.ProfileEffect, d.Bio, d.Pronouns))
            .FirstOrDefaultAsync(ct);

        if (decoration == null) return new DecorationResponse(0, request.UserId, null, null, null, null, null, null);
        return decoration;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/users/{userId}/decorations", async (
            long userId,
            IRequestHandler<GetDecorationQuery, Result<DecorationResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new GetDecorationQuery(userId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("GetDecoration").WithTags("ProfileDecorations");
}
