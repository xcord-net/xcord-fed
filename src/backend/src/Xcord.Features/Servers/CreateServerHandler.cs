using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;

namespace Xcord.Features.Servers;

public sealed record CreateServerCommand(
    string Name,
    string? Description,
    string? IconUrl,
    string? BannerUrl,
    string? PreferredLocale
);

public sealed class CreateServerHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IHttpContextAccessor httpContextAccessor,
    IOptions<TierOptions> tierOptions,
    ILogger<CreateServerHandler> logger)
    : IRequestHandler<CreateServerCommand, Result<CreateServerResponse>>, IValidatable<CreateServerCommand>
{
    public Error? Validate(CreateServerCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("VALIDATION_ERROR", "Server name is required");
        }

        if (request.Name.Length > 100)
        {
            return Error.Validation("VALIDATION_ERROR", "Server name must not exceed 100 characters");
        }

        if (request.Description != null && request.Description.Length > 1024)
        {
            return Error.Validation("VALIDATION_ERROR", "Description must not exceed 1024 characters");
        }

        if (request.IconUrl != null && request.IconUrl.Length > 512)
        {
            return Error.Validation("VALIDATION_ERROR", "Icon URL must not exceed 512 characters");
        }

        if (request.BannerUrl != null && request.BannerUrl.Length > 512)
        {
            return Error.Validation("VALIDATION_ERROR", "Banner URL must not exceed 512 characters");
        }

        if (request.PreferredLocale != null && request.PreferredLocale.Length > 10)
        {
            return Error.Validation("VALIDATION_ERROR", "Preferred locale must not exceed 10 characters");
        }

        return null;
    }

    public async Task<Result<CreateServerResponse>> Handle(CreateServerCommand request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Tier gating: enforce server capacity limit (0 = unlimited)
        var maxServers = tierOptions.Value.MaxServers;
        if (maxServers > 0)
        {
            var serverCount = await dbContext.Servers
                .CountAsync(s => s.DeletedAt == null, cancellationToken);

            if (serverCount >= maxServers)
            {
                return Error.Forbidden("CAPACITY_EXCEEDED", "This instance has reached its maximum server capacity");
            }
        }

        var now = DateTimeOffset.UtcNow;

        // Create server (SystemChannelId is set after the channel is created to avoid circular FK)
        var serverId = snowflakeGenerator.NextId();

        var server = new Server
        {
            Id = serverId,
            Name = request.Name,
            Description = request.Description,
            IconUrl = request.IconUrl,
            BannerUrl = request.BannerUrl,
            OwnerId = userId,
            MemberCount = 1, // Owner is the first member
            PreferredLocale = request.PreferredLocale,
            CreatedAt = now
        };

        dbContext.Servers.Add(server);

        // Create ServerMember for the owner
        var serverMember = new ServerMember
        {
            UserId = userId,
            ServerId = serverId,
            JoinedAt = now
        };

        dbContext.ServerMembers.Add(serverMember);

        // Create @everyone role with default permissions
        var everyoneRoleId = snowflakeGenerator.NextId();
        var everyoneRole = new Role
        {
            Id = everyoneRoleId,
            ServerId = serverId,
            Name = "@everyone",
            Color = null,
            Permissions = (long)(Permission.ViewChannels | Permission.SendMessages |
                                Permission.EmbedLinks | Permission.AttachFiles |
                                Permission.ReadMessageHistory | Permission.AddReactions |
                                Permission.Connect | Permission.Speak |
                                Permission.CreatePublicThreads | Permission.SendMessagesInThreads),
            Position = 0,
            IsEveryone = true,
            CreatedAt = now
        };

        dbContext.Roles.Add(everyoneRole);

        // Create "General" category
        var generalCategoryId = snowflakeGenerator.NextId();
        var generalCategory = new Category
        {
            Id = generalCategoryId,
            ServerId = serverId,
            Name = "General",
            Position = 0,
            CreatedAt = now
        };

        dbContext.Categories.Add(generalCategory);

        // Create "general" text channel conversation
        var generalConversationId = snowflakeGenerator.NextId();
        var generalConversation = new Conversation
        {
            Id = generalConversationId,
            Type = ConversationType.Channel
        };

        dbContext.Conversations.Add(generalConversation);

        // Create "general" text channel (also used as the default system channel)
        var generalChannelId = snowflakeGenerator.NextId();
        var generalChannel = new Channel
        {
            Id = generalChannelId,
            ConversationId = generalConversationId,
            ServerId = serverId,
            CategoryId = generalCategoryId,
            Name = "general",
            Topic = null,
            Type = ChannelType.Text,
            Position = 0,
            SlowModeSeconds = null,
            IsNsfw = false,
            DefaultSortOrder = null,
            RequireTag = false,
            DefaultAutoArchiveDuration = null,
            CreatedAt = now
        };

        dbContext.Channels.Add(generalChannel);

        // Create ReadState for the server owner so the unread notification
        // system can track messages in the initial channel.
        dbContext.ReadStates.Add(new Xcord.Entities.ReadState
        {
            UserId = userId,
            ConversationId = generalConversationId,
            UnreadCount = 0,
            MentionCount = 0
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        // Set the general channel as the system channel after both server and channel are persisted.
        // This must happen in a second SaveChanges to avoid a circular FK constraint violation:
        // servers.SystemChannelId -> channels.Id, but channels.ServerId -> servers.Id.
        server.SystemChannelId = generalChannelId;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} created server {ServerName} (ID: {ServerId})",
            userId, server.Name, serverId);

        return new CreateServerResponse(
            Id: server.Id,
            Name: server.Name,
            Description: server.Description,
            IconUrl: server.IconUrl,
            BannerUrl: server.BannerUrl,
            OwnerId: server.OwnerId,
            MemberCount: server.MemberCount,
            PreferredLocale: server.PreferredLocale,
            CreatedAt: server.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers", async (
            CreateServerCommand request,
            IRequestHandler<CreateServerCommand, Result<CreateServerResponse>> handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(request, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateServer")
        .WithTags("Servers");
    }
}
