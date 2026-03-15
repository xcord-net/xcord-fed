using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Channels;

public sealed record ListChannelsCommand(long ServerId);

public sealed record ListChannelsResponse(
    List<ChannelDto> Channels,
    List<CategoryDto> Categories
);

public sealed record CategoryDto(
    long Id,
    long ServerId,
    string Name,
    int Position,
    DateTimeOffset CreatedAt
);

public sealed record ChannelDto(
    long Id,
    long ConversationId,
    long ServerId,
    long? CategoryId,
    string Name,
    string? Topic,
    ChannelType Type,
    int Position,
    int? SlowModeSeconds,
    bool IsNsfw,
    ForumSort? DefaultSortOrder,
    bool RequireTag,
    int? DefaultAutoArchiveDuration,
    DateTimeOffset CreatedAt
);

public sealed class ListChannelsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService)
    : IRequestHandler<ListChannelsCommand, Result<ListChannelsResponse>>
{
    private const int MaxChannelsPerServer = 500;

    public async Task<Result<ListChannelsResponse>> Handle(ListChannelsCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Check if server exists
        var serverExists = await dbContext.Servers
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.ServerId, cancellationToken);

        if (!serverExists)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
        }

        // Check if user is a member
        var memberCheck = await dbContext.EnsureMembership(request.ServerId, userId, cancellationToken);
        if (memberCheck.IsFailure) return memberCheck.Error;

        // Get server-level permissions to determine if the user is an admin/owner
        // (admins see all channels regardless of overrides)
        var serverPerms = await roleService.GetServerRoles(userId, request.ServerId);
        var isAdmin = serverPerms == long.MaxValue ||
                      (serverPerms & (long)Role.Administrator) != 0;

        // Get all channels for this server, ordered by position
        var allChannels = await dbContext.Channels
            .AsNoTracking()
            .Where(c => c.ServerId == request.ServerId)
            .OrderBy(c => c.Position)
            .Take(MaxChannelsPerServer)
            .Select(c => new ChannelDto(
                c.Id,
                c.ConversationId,
                c.ServerId,
                c.CategoryId,
                c.Name,
                c.Topic,
                c.Type,
                c.Position,
                c.SlowModeSeconds,
                c.IsNsfw,
                c.DefaultSortOrder,
                c.RequireTag,
                c.DefaultAutoArchiveDuration,
                c.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        // Get all categories for this server
        var categories = await dbContext.Categories
            .AsNoTracking()
            .Where(cat => cat.ServerId == request.ServerId)
            .OrderBy(cat => cat.Position)
            .Select(cat => new CategoryDto(
                cat.Id,
                cat.ServerId,
                cat.Name,
                cat.Position,
                cat.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        // Admins and server owners see all channels regardless of ViewChannel overrides
        if (isAdmin)
        {
            return new ListChannelsResponse(Channels: allChannels, Categories: categories);
        }

        // For regular members, filter out channels where they lack ViewChannel permission
        var visibleChannels = new List<ChannelDto>(allChannels.Count);
        foreach (var channel in allChannels)
        {
            var channelPerms = await roleService.GetChannelRoles(userId, channel.Id);
            if ((channelPerms & (long)Role.ViewChannels) != 0)
            {
                visibleChannels.Add(channel);
            }
        }

        return new ListChannelsResponse(Channels: visibleChannels, Categories: categories);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/servers/{serverId}/channels", async (
            long serverId,
            [FromServices] ListChannelsHandler handler,
            CancellationToken ct) =>
        {
            var command = new ListChannelsCommand(serverId);

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListChannels")
        .WithTags("Channels");
    }
}
