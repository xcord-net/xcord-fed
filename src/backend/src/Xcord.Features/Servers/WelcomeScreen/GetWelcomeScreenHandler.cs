using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Servers.WelcomeScreen;

public sealed record GetWelcomeScreenQuery(long ServerId);
public sealed record WelcomeChannelDto(long ChannelId, string? Description, string? EmojiName, int Position);
public sealed record WelcomeScreenResponse(long Id, long ServerId, string? Description, bool IsEnabled, List<WelcomeChannelDto> Channels);

public sealed class GetWelcomeScreenHandler(AppDbContext dbContext)
    : IRequestHandler<GetWelcomeScreenQuery, Result<WelcomeScreenResponse>>
{
    public async Task<Result<WelcomeScreenResponse>> Handle(GetWelcomeScreenQuery request, CancellationToken ct)
    {
        var screen = await dbContext.WelcomeScreens.AsNoTracking()
            .Include(w => w.Channels.OrderBy(c => c.Position))
            .FirstOrDefaultAsync(w => w.ServerId == request.ServerId, ct);

        if (screen == null)
            return new WelcomeScreenResponse(0, request.ServerId, null, false, new List<WelcomeChannelDto>());

        var channels = screen.Channels.Select(c => new WelcomeChannelDto(c.ChannelId, c.Description, c.EmojiName, c.Position)).ToList();
        return new WelcomeScreenResponse(screen.Id, screen.ServerId, screen.Description, screen.IsEnabled, channels);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/servers/{serverId}/welcome-screen", async (
            long serverId,
            IRequestHandler<GetWelcomeScreenQuery, Result<WelcomeScreenResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new GetWelcomeScreenQuery(serverId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("GetWelcomeScreen").WithTags("WelcomeScreen");
}
