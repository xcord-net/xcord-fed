using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Servers;

public sealed record GetVanityUrlQuery(long ServerId);
public sealed record VanityUrlResponse(long ServerId, string? Slug, string? VanityUrl);

public sealed class GetVanityUrlHandler(AppDbContext dbContext)
    : IRequestHandler<GetVanityUrlQuery, Result<VanityUrlResponse>>
{
    public async Task<Result<VanityUrlResponse>> Handle(GetVanityUrlQuery request, CancellationToken ct)
    {
        var server = await dbContext.Servers.AsNoTracking()
            .Where(s => s.Id == request.ServerId)
            .Select(s => new { s.Id, s.VanitySlug })
            .FirstOrDefaultAsync(ct);

        if (server == null) return Error.NotFound("SERVER_NOT_FOUND", "Server not found");

        var vanityUrl = server.VanitySlug != null ? $"/invite/{server.VanitySlug}" : null;
        return new VanityUrlResponse(server.Id, server.VanitySlug, vanityUrl);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/servers/{serverId}/vanity-url", async (
            long serverId,
            IRequestHandler<GetVanityUrlQuery, Result<VanityUrlResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new GetVanityUrlQuery(serverId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("GetVanityUrl").WithTags("VanityUrl");
}
