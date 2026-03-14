using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Servers;

public sealed record UpdateWelcomeScreenCommand(long ServerId, string? Description, bool IsEnabled, List<WelcomeChannelDto> Channels);
public sealed record UpdateWelcomeScreenRequest(string? Description, bool IsEnabled, List<WelcomeChannelDto> Channels);

public sealed class UpdateWelcomeScreenHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService, IRoleService roleService)
    : IRequestHandler<UpdateWelcomeScreenCommand, Result<WelcomeScreenResponse>>
{
    public async Task<Result<WelcomeScreenResponse>> Handle(UpdateWelcomeScreenCommand request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var perm = await roleService.EnsureServerRole(userId, request.ServerId, Role.ManageServer);
        if (perm.IsFailure) return Error.Forbidden("MISSING_PERMISSIONS", "You do not have permission");

        var now = DateTimeOffset.UtcNow;
        var screen = await dbContext.WelcomeScreens.Include(w => w.Channels)
            .FirstOrDefaultAsync(w => w.ServerId == request.ServerId, ct);

        if (screen == null)
        {
            screen = new Entities.WelcomeScreen
            {
                Id = snowflakeGenerator.NextId(), ServerId = request.ServerId,
                Description = request.Description, IsEnabled = request.IsEnabled, CreatedAt = now, UpdatedAt = now
            };
            dbContext.WelcomeScreens.Add(screen);
        }
        else
        {
            screen.Description = request.Description;
            screen.IsEnabled = request.IsEnabled;
            screen.UpdatedAt = now;
            // Remove old channels
            foreach (var ch in screen.Channels.ToList())
                ch.DeletedAt = now;
        }

        // Add new channels
        foreach (var ch in request.Channels)
        {
            dbContext.WelcomeScreenChannels.Add(new WelcomeScreenChannel
            {
                Id = snowflakeGenerator.NextId(), WelcomeScreenId = screen.Id,
                ChannelId = ch.ChannelId, Description = ch.Description,
                EmojiName = ch.EmojiName, Position = ch.Position
            });
        }

        await dbContext.SaveChangesAsync(ct);

        var channels = request.Channels;
        return new WelcomeScreenResponse(screen.Id, screen.ServerId, screen.Description, screen.IsEnabled, channels);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/servers/{serverId}/welcome-screen", async (
            long serverId, UpdateWelcomeScreenRequest request,
            IRequestHandler<UpdateWelcomeScreenCommand, Result<WelcomeScreenResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new UpdateWelcomeScreenCommand(serverId, request.Description, request.IsEnabled, request.Channels), ct))
        .RequireAuthorization(Policies.User)
        .WithName("UpdateWelcomeScreen").WithTags("WelcomeScreen");
}
