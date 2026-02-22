using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Servers.Soundboard;

public sealed record ListSoundsQuery(long ServerId);
public sealed record SoundResponse(long Id, string Name, long ServerId, long UploadedBy, int DurationMs, bool IsDefault, DateTimeOffset CreatedAt);

public sealed class ListSoundsHandler(AppDbContext dbContext)
    : IRequestHandler<ListSoundsQuery, Result<List<SoundResponse>>>
{
    public async Task<Result<List<SoundResponse>>> Handle(ListSoundsQuery request, CancellationToken ct)
    {
        var sounds = await dbContext.SoundboardSounds.AsNoTracking()
            .Where(s => s.ServerId == request.ServerId)
            .OrderBy(s => s.Name)
            .Take(50)
            .Select(s => new SoundResponse(s.Id, s.Name, s.ServerId, s.UploadedByUserId, s.DurationMs, s.IsDefault, s.CreatedAt))
            .ToListAsync(ct);
        return sounds;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/servers/{serverId}/sounds", async (
            long serverId,
            IRequestHandler<ListSoundsQuery, Result<List<SoundResponse>>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new ListSoundsQuery(serverId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("ListSounds").WithTags("Soundboard");
}
