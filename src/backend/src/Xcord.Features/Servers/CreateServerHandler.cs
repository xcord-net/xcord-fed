using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

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
    ICurrentUserService currentUserService,
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
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Tier gating: enforce server capacity limit (0 = unlimited).
        // Instance admin (operator) is exempt - the limit is for tenants on a SaaS
        // plan, not the operator running the instance.
        var isAdmin = httpContextAccessor.HttpContext?.User.HasClaim(c => c.Type == "admin" && c.Value == "true") ?? false;
        var maxServers = tierOptions.Value.MaxServers;
        if (maxServers > 0 && !isAdmin)
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

        // Create @everyone group with default roles
        var everyoneGroupId = snowflakeGenerator.NextId();
        var everyoneGroup = new Group
        {
            Id = everyoneGroupId,
            ServerId = serverId,
            Name = "@everyone",
            Color = null,
            Roles = (long)(Role.ViewChannels | Role.SendMessages |
                           Role.EmbedLinks | Role.AttachFiles |
                           Role.ReadMessageHistory | Role.AddReactions |
                           Role.Connect | Role.Speak |
                           Role.CreatePublicThreads | Role.SendMessagesInThreads),
            Position = 0,
            IsEveryone = true,
            CreatedAt = now
        };

        dbContext.Groups.Add(everyoneGroup);

        // Create default Member group
        var memberGroupId = snowflakeGenerator.NextId();
        var memberGroup = new Group
        {
            Id = memberGroupId,
            ServerId = serverId,
            Name = "Member",
            Roles = (long)(Role.ViewChannels | Role.SendMessages | Role.EmbedLinks |
                           Role.AttachFiles | Role.ReadMessageHistory | Role.AddReactions |
                           Role.Connect | Role.Speak | Role.CreatePublicThreads |
                           Role.SendMessagesInThreads | Role.UseExternalEmojis |
                           Role.ChangeNickname | Role.Video | Role.ViewBroadcast),
            Position = 1,
            CreatedAt = now
        };
        dbContext.Groups.Add(memberGroup);

        // Create default Moderator group
        var moderatorGroupId = snowflakeGenerator.NextId();
        var moderatorGroup = new Group
        {
            Id = moderatorGroupId,
            ServerId = serverId,
            Name = "Moderator",
            Color = "#e06a8a",
            Roles = memberGroup.Roles | (long)(Role.ManageMessages | Role.KickMembers |
                                               Role.BanMembers | Role.TimeoutMembers |
                                               Role.ManageEmojis | Role.ManageStickers |
                                               Role.ManageNicknames),
            Position = 2,
            CreatedAt = now
        };
        dbContext.Groups.Add(moderatorGroup);

        // Create default Bot group
        var botGroupId = snowflakeGenerator.NextId();
        var botGroup = new Group
        {
            Id = botGroupId,
            ServerId = serverId,
            Name = "Bot",
            Color = "#7289da",
            Roles = (long)(Role.SendMessages | Role.EmbedLinks | Role.AttachFiles |
                           Role.ReadMessageHistory | Role.AddReactions |
                           Role.Connect | Role.Speak),
            Position = 3,
            CreatedAt = now
        };
        dbContext.Groups.Add(botGroup);

        // Create default tiers for monetized servers
        if (tierOptions.Value.CanUseMemberTiers)
        {
            // Create Pro group for tier subscribers
            var proGroupId = snowflakeGenerator.NextId();
            var proGroup = new Group
            {
                Id = proGroupId,
                ServerId = serverId,
                Name = "Pro",
                Color = "#f1c40f",
                Roles = memberGroup.Roles | (long)(Role.CreatePrivateThreads | Role.ShareScreen),
                Position = 4,
                LimitsJson = """{"CreatePrivateThreads": 5}""",
                CreatedAt = now
            };
            dbContext.Groups.Add(proGroup);

            // Create VIP group
            var vipGroupId = snowflakeGenerator.NextId();
            var vipGroup = new Group
            {
                Id = vipGroupId,
                ServerId = serverId,
                Name = "VIP",
                Color = "#9b59b6",
                Roles = proGroup.Roles,
                Position = 5,
                LimitsJson = """{"CreateEncryptedChannel": 3, "MaxFileUploadMb": 50}""",
                CreatedAt = now
            };
            dbContext.Groups.Add(vipGroup);

            // Create default tiers
            var supporterTier = new Tier
            {
                Id = snowflakeGenerator.NextId(),
                ServerId = serverId,
                Name = "Supporter",
                Description = "Support the server and get the Member group",
                PriceMonthly = 499,
                GroupIdsJson = System.Text.Json.JsonSerializer.Serialize(new[] { memberGroupId }),
                Position = 0,
                CreatedAt = now
            };
            dbContext.Tiers.Add(supporterTier);

            var proTier = new Tier
            {
                Id = snowflakeGenerator.NextId(),
                ServerId = serverId,
                Name = "Pro",
                Description = "Pro features including private threads and screen sharing",
                PriceMonthly = 999,
                GroupIdsJson = System.Text.Json.JsonSerializer.Serialize(new[] { memberGroupId, proGroupId }),
                Position = 1,
                CreatedAt = now
            };
            dbContext.Tiers.Add(proTier);

            var vipTier = new Tier
            {
                Id = snowflakeGenerator.NextId(),
                ServerId = serverId,
                Name = "VIP",
                Description = "All Pro features plus encrypted channels and larger uploads",
                PriceMonthly = 1999,
                GroupIdsJson = System.Text.Json.JsonSerializer.Serialize(new[] { memberGroupId, proGroupId, vipGroupId }),
                Position = 2,
                CreatedAt = now
            };
            dbContext.Tiers.Add(vipTier);
        }

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
            Capabilities = ChannelCapability.Chat,
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

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Set the general channel as the system channel after both server and channel are persisted.
        // This must happen in a second SaveChanges to avoid a circular FK constraint violation:
        // servers.SystemChannelId -> channels.Id, but channels.ServerId -> servers.Id.
        server.SystemChannelId = generalChannelId;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
            return await handler.ExecuteAsync(request, ct, success => Results.Created($"/api/v1/servers/{success.Id}", success)).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateServer")
        .WithTags("Servers");
    }
}
