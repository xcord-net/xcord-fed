using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Channels.Stage;

public sealed record ListStageSpeakersQuery(long ChannelId);

public sealed class ListStageSpeakersHandler(AppDbContext dbContext)
    : IRequestHandler<ListStageSpeakersQuery, Result<List<StageSpeakerResponse>>>
{
    public async Task<Result<List<StageSpeakerResponse>>> Handle(ListStageSpeakersQuery request, CancellationToken ct)
    {
        var session = await dbContext.StageSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ChannelId == request.ChannelId && s.EndedAt == null, ct);
        if (session == null) return new List<StageSpeakerResponse>();

        var speakers = await dbContext.StageSpeakers.AsNoTracking()
            .Where(s => s.StageSessionId == session.Id)
            .Select(s => new StageSpeakerResponse(s.Id, s.UserId, s.Role.ToString(), s.RequestedAt))
            .ToListAsync(ct);

        return speakers;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/channels/{channelId}/stage/speakers", async (
            long channelId,
            IRequestHandler<ListStageSpeakersQuery, Result<List<StageSpeakerResponse>>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new ListStageSpeakersQuery(channelId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("ListStageSpeakers").WithTags("Stage");
}
