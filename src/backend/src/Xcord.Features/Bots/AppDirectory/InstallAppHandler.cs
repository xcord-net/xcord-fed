using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Bots;

public sealed record InstallAppCommand(long AppId, long ServerId);
public sealed record InstallAppRequest(long ServerId);
public sealed record InstallAppResponse(long AppId, string Status);

public sealed class InstallAppHandler(AppDbContext dbContext)
    : IRequestHandler<InstallAppCommand, Result<InstallAppResponse>>
{
    public async Task<Result<InstallAppResponse>> Handle(InstallAppCommand request, CancellationToken ct)
    {
        var app = await dbContext.AppListings.FirstOrDefaultAsync(a => a.Id == request.AppId && a.IsPublished, ct);
        if (app == null) return Error.NotFound("APP_NOT_FOUND", "App not found");

        app.InstallCount++;
        await dbContext.SaveChangesAsync(ct);
        return new InstallAppResponse(app.Id, "installed");
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder appBuilder) =>
        appBuilder.MapPost("/api/v1/app-directory/{appId}/install", async (
            long appId, InstallAppRequest request,
            IRequestHandler<InstallAppCommand, Result<InstallAppResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new InstallAppCommand(appId, request.ServerId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("InstallApp").WithTags("AppDirectory");
}
