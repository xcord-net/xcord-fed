using Microsoft.EntityFrameworkCore;
using Xcord;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailConfirmationToken> EmailConfirmationTokens => Set<EmailConfirmationToken>();
    public DbSet<TwoFactorCode> TwoFactorCodes => Set<TwoFactorCode>();
    public DbSet<TwoFactorBackupCode> TwoFactorBackupCodes => Set<TwoFactorBackupCode>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<EncryptedDataKey> EncryptedDataKeys => Set<EncryptedDataKey>();
    public DbSet<BotToken> BotTokens => Set<BotToken>();
    public DbSet<Webhook> Webhooks => Set<Webhook>();
    public DbSet<Server> Servers => Set<Server>();
    public DbSet<ServerMember> ServerMembers => Set<ServerMember>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<MemberGroup> MemberGroups => Set<MemberGroup>();
    public DbSet<ChannelPermissionOverride> ChannelPermissionOverrides => Set<ChannelPermissionOverride>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Mention> Mentions => Set<Mention>();
    public DbSet<Reaction> Reactions => Set<Reaction>();
    public DbSet<MessageEdit> MessageEdits => Set<MessageEdit>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Embed> Embeds => Set<Embed>();
    public DbSet<Xcord.Entities.Thread> Threads => Set<Xcord.Entities.Thread>();
    public DbSet<ThreadMember> ThreadMembers => Set<ThreadMember>();
    public DbSet<DmChannel> DmChannels => Set<DmChannel>();
    public DbSet<DmChannelMember> DmChannelMembers => Set<DmChannelMember>();
public DbSet<VoiceState> VoiceStates => Set<VoiceState>();
    public DbSet<ReadState> ReadStates => Set<ReadState>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<CustomEmoji> CustomEmojis => Set<CustomEmoji>();
    public DbSet<StickerPack> StickerPacks => Set<StickerPack>();
    public DbSet<Sticker> Stickers => Set<Sticker>();
    public DbSet<NotificationSetting> NotificationSettings => Set<NotificationSetting>();
    public DbSet<ForumTag> ForumTags => Set<ForumTag>();
    public DbSet<ForumPostTag> ForumPostTags => Set<ForumPostTag>();
    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<PollOption> PollOptions => Set<PollOption>();
    public DbSet<PollVote> PollVotes => Set<PollVote>();
    public DbSet<ScheduledEvent> ScheduledEvents => Set<ScheduledEvent>();
    public DbSet<EventRsvp> EventRsvps => Set<EventRsvp>();
    public DbSet<AutomodRule> AutomodRules => Set<AutomodRule>();
    public DbSet<Call> Calls => Set<Call>();
    public DbSet<Entities.Timeout> Timeouts => Set<Entities.Timeout>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Ban> Bans => Set<Ban>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CrosspostSubscription> CrosspostSubscriptions => Set<CrosspostSubscription>();

    // New entities for sprint cards 176-200
    public DbSet<ServerTemplate> ServerTemplates => Set<ServerTemplate>();
    public DbSet<SlashCommand> SlashCommands => Set<SlashCommand>();
    public DbSet<MessageComponent> MessageComponents => Set<MessageComponent>();
    public DbSet<AppListing> AppListings => Set<AppListing>();
    public DbSet<AppReview> AppReviews => Set<AppReview>();
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();
    public DbSet<WelcomeScreen> WelcomeScreens => Set<WelcomeScreen>();
    public DbSet<WelcomeScreenChannel> WelcomeScreenChannels => Set<WelcomeScreenChannel>();
    public DbSet<OnboardingConfig> OnboardingConfigs => Set<OnboardingConfig>();
    public DbSet<OnboardingPrompt> OnboardingPrompts => Set<OnboardingPrompt>();
    public DbSet<OnboardingCompletion> OnboardingCompletions => Set<OnboardingCompletion>();
    public DbSet<ServerInsightSnapshot> ServerInsightSnapshots => Set<ServerInsightSnapshot>();

    // New entities for feature cards 193-198
    public DbSet<FederationFollow> FederationFollows => Set<FederationFollow>();
    public DbSet<FederationMessage> FederationMessages => Set<FederationMessage>();
    public DbSet<UserNote> UserNotes => Set<UserNote>();

    // Outgoing webhooks
    public DbSet<OutgoingWebhook> OutgoingWebhooks => Set<OutgoingWebhook>();
    public DbSet<OutgoingWebhookDelivery> OutgoingWebhookDeliveries => Set<OutgoingWebhookDelivery>();

    // Scheduled messages
    public DbSet<ScheduledMessage> ScheduledMessages => Set<ScheduledMessage>();

    // Discord migration
    public DbSet<DiscordMigration> DiscordMigrations => Set<DiscordMigration>();
    public DbSet<DiscordIdMapping> DiscordIdMappings => Set<DiscordIdMapping>();

    // Broadcast / streaming
    public DbSet<Broadcast> Broadcasts => Set<Broadcast>();
    public DbSet<BroadcastStageSlot> BroadcastStageSlots => Set<BroadcastStageSlot>();
    public DbSet<StreamBot> StreamBots => Set<StreamBot>();
    public DbSet<BroadcastStreambot> BroadcastStreambots => Set<BroadcastStreambot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var property = System.Linq.Expressions.Expression.Property(parameter, nameof(ISoftDeletable.DeletedAt));
                var nullConstant = System.Linq.Expressions.Expression.Constant(null, typeof(DateTimeOffset?));
                var comparison = System.Linq.Expressions.Expression.Equal(property, nullConstant);
                var lambda = System.Linq.Expressions.Expression.Lambda(comparison, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }
}
