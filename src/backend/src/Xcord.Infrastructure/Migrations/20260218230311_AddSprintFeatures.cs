using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledDeletionAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BoostCount",
                table: "servers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BoostLevel",
                table: "servers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VanitySlug",
                table: "servers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuper",
                table: "reactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "app_listings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    BotTokenId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ShortDescription = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IconUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InstallCount = table.Column<int>(type: "integer", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_listings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_app_listings_bot_tokens_BotTokenId",
                        column: x => x.BotTokenId,
                        principalTable: "bot_tokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "connected_accounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderAccountId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProviderUsername = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AccessToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RefreshToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    ShowOnProfile = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connected_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_connected_accounts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "crosspost_subscriptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    SourceChannelId = table.Column<long>(type: "bigint", nullable: false),
                    TargetChannelId = table.Column<long>(type: "bigint", nullable: false),
                    SourceServerId = table.Column<long>(type: "bigint", nullable: false),
                    TargetServerId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crosspost_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crosspost_subscriptions_channels_SourceChannelId",
                        column: x => x.SourceChannelId,
                        principalTable: "channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_crosspost_subscriptions_channels_TargetChannelId",
                        column: x => x.TargetChannelId,
                        principalTable: "channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "federation_follows",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    RemoteInstanceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LocalChannelId = table.Column<long>(type: "bigint", nullable: false),
                    RemoteChannelId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RemoteChannelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FollowedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_federation_follows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_federation_follows_channels_LocalChannelId",
                        column: x => x.LocalChannelId,
                        principalTable: "channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_federation_follows_users_FollowedByUserId",
                        column: x => x.FollowedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "message_components",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    ComponentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CustomId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Style = table.Column<int>(type: "integer", nullable: true),
                    OptionsJson = table.Column<string>(type: "jsonb", nullable: true),
                    Disabled = table.Column<bool>(type: "boolean", nullable: false),
                    Row = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_components", x => x.Id);
                    table.ForeignKey(
                        name: "FK_message_components_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "onboarding_completions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResponseDataJson = table.Column<string>(type: "jsonb", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_completions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_onboarding_completions_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_onboarding_completions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "onboarding_configs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultChannelIds = table.Column<string>(type: "jsonb", nullable: true),
                    RulesText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_configs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_onboarding_configs_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outgoing_webhooks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Secret = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventTypes = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    LastFailureAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outgoing_webhooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_outgoing_webhooks_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profile_decorations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    BannerUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    BannerColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    AvatarFrameUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ProfileEffect = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Bio = table.Column<string>(type: "character varying(190)", maxLength: 190, nullable: true),
                    Pronouns = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_decorations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_profile_decorations_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_messages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorId = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsSent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scheduled_messages_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_scheduled_messages_users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "server_boosts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_server_boosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_server_boosts_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_server_boosts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "server_insight_snapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalMembers = table.Column<int>(type: "integer", nullable: false),
                    NewMembers = table.Column<int>(type: "integer", nullable: false),
                    MembersLeft = table.Column<int>(type: "integer", nullable: false),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
                    ActiveMembers = table.Column<int>(type: "integer", nullable: false),
                    TopChannelIds = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_server_insight_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_server_insight_snapshots_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "server_templates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    SourceServerId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ChannelData = table.Column<string>(type: "jsonb", nullable: false),
                    RoleData = table.Column<string>(type: "jsonb", nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_server_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_server_templates_servers_SourceServerId",
                        column: x => x.SourceServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "slash_commands",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    BotTokenId = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OptionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slash_commands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_slash_commands_bot_tokens_BotTokenId",
                        column: x => x.BotTokenId,
                        principalTable: "bot_tokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_slash_commands_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "soundboard_sounds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    S3Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AudioUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_soundboard_sounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_soundboard_sounds_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_soundboard_sounds_users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stage_sessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    Topic = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stage_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stage_sessions_channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_activities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ActivityType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Details = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    State = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LargeImageUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SmallImageUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_activities_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_notes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    AuthorId = table.Column<long>(type: "bigint", nullable: false),
                    TargetUserId = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_notes_users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_notes_users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "welcome_screens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_welcome_screens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_welcome_screens_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "app_reviews",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    AppListingId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_app_reviews_app_listings_AppListingId",
                        column: x => x.AppListingId,
                        principalTable: "app_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_app_reviews_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "federation_messages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    FederationFollowId = table.Column<long>(type: "bigint", nullable: false),
                    RemoteMessageId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LocalMessageId = table.Column<long>(type: "bigint", nullable: false),
                    RemoteAuthorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RemoteAuthorAvatarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_federation_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_federation_messages_federation_follows_FederationFollowId",
                        column: x => x.FederationFollowId,
                        principalTable: "federation_follows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_federation_messages_messages_LocalMessageId",
                        column: x => x.LocalMessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "onboarding_prompts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OnboardingConfigId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    OptionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_prompts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_onboarding_prompts_onboarding_configs_OnboardingConfigId",
                        column: x => x.OnboardingConfigId,
                        principalTable: "onboarding_configs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outgoing_webhook_deliveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OutgoingWebhookId = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    ResponseBody = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outgoing_webhook_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_outgoing_webhook_deliveries_outgoing_webhooks_OutgoingWebho~",
                        column: x => x.OutgoingWebhookId,
                        principalTable: "outgoing_webhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stage_speakers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    StageSessionId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stage_speakers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stage_speakers_stage_sessions_StageSessionId",
                        column: x => x.StageSessionId,
                        principalTable: "stage_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stage_speakers_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "welcome_screen_channels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    WelcomeScreenId = table.Column<long>(type: "bigint", nullable: false),
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmojiName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_welcome_screen_channels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_welcome_screen_channels_channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_welcome_screen_channels_welcome_screens_WelcomeScreenId",
                        column: x => x.WelcomeScreenId,
                        principalTable: "welcome_screens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_servers_VanitySlug",
                table: "servers",
                column: "VanitySlug",
                unique: true,
                filter: "\"VanitySlug\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_app_listings_BotTokenId",
                table: "app_listings",
                column: "BotTokenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_listings_IsPublished",
                table: "app_listings",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_app_reviews_AppListingId_UserId",
                table: "app_reviews",
                columns: new[] { "AppListingId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_reviews_UserId",
                table: "app_reviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_connected_accounts_UserId_Provider",
                table: "connected_accounts",
                columns: new[] { "UserId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_crosspost_subscriptions_SourceChannelId_TargetChannelId",
                table: "crosspost_subscriptions",
                columns: new[] { "SourceChannelId", "TargetChannelId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_crosspost_subscriptions_TargetChannelId",
                table: "crosspost_subscriptions",
                column: "TargetChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_federation_follows_FollowedByUserId",
                table: "federation_follows",
                column: "FollowedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_federation_follows_LocalChannelId",
                table: "federation_follows",
                column: "LocalChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_federation_follows_LocalChannelId_RemoteInstanceUrl_RemoteC~",
                table: "federation_follows",
                columns: new[] { "LocalChannelId", "RemoteInstanceUrl", "RemoteChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_federation_messages_FederationFollowId_RemoteMessageId",
                table: "federation_messages",
                columns: new[] { "FederationFollowId", "RemoteMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_federation_messages_LocalMessageId",
                table: "federation_messages",
                column: "LocalMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_message_components_MessageId",
                table: "message_components",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_completions_ServerId_UserId",
                table: "onboarding_completions",
                columns: new[] { "ServerId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_completions_UserId",
                table: "onboarding_completions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_configs_ServerId",
                table: "onboarding_configs",
                column: "ServerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_prompts_OnboardingConfigId",
                table: "onboarding_prompts",
                column: "OnboardingConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_outgoing_webhook_deliveries_OutgoingWebhookId",
                table: "outgoing_webhook_deliveries",
                column: "OutgoingWebhookId");

            migrationBuilder.CreateIndex(
                name: "IX_outgoing_webhooks_ServerId",
                table: "outgoing_webhooks",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_profile_decorations_UserId",
                table: "profile_decorations",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_messages_AuthorId",
                table: "scheduled_messages",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_messages_ConversationId_AuthorId",
                table: "scheduled_messages",
                columns: new[] { "ConversationId", "AuthorId" });

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_messages_IsSent_ScheduledAt",
                table: "scheduled_messages",
                columns: new[] { "IsSent", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_server_boosts_ServerId",
                table: "server_boosts",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_server_boosts_ServerId_UserId",
                table: "server_boosts",
                columns: new[] { "ServerId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_server_boosts_UserId",
                table: "server_boosts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_server_insight_snapshots_ServerId_Date",
                table: "server_insight_snapshots",
                columns: new[] { "ServerId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_server_templates_SourceServerId",
                table: "server_templates",
                column: "SourceServerId");

            migrationBuilder.CreateIndex(
                name: "IX_slash_commands_BotTokenId",
                table: "slash_commands",
                column: "BotTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_slash_commands_ServerId_Name",
                table: "slash_commands",
                columns: new[] { "ServerId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_soundboard_sounds_ServerId",
                table: "soundboard_sounds",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_soundboard_sounds_UploadedByUserId",
                table: "soundboard_sounds",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stage_sessions_ChannelId",
                table: "stage_sessions",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_stage_speakers_StageSessionId_UserId",
                table: "stage_speakers",
                columns: new[] { "StageSessionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stage_speakers_UserId",
                table: "stage_speakers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_activities_UserId",
                table: "user_activities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_notes_AuthorId_TargetUserId",
                table: "user_notes",
                columns: new[] { "AuthorId", "TargetUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_notes_TargetUserId",
                table: "user_notes",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_welcome_screen_channels_ChannelId",
                table: "welcome_screen_channels",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_welcome_screen_channels_WelcomeScreenId",
                table: "welcome_screen_channels",
                column: "WelcomeScreenId");

            migrationBuilder.CreateIndex(
                name: "IX_welcome_screens_ServerId",
                table: "welcome_screens",
                column: "ServerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_reviews");

            migrationBuilder.DropTable(
                name: "connected_accounts");

            migrationBuilder.DropTable(
                name: "crosspost_subscriptions");

            migrationBuilder.DropTable(
                name: "federation_messages");

            migrationBuilder.DropTable(
                name: "message_components");

            migrationBuilder.DropTable(
                name: "onboarding_completions");

            migrationBuilder.DropTable(
                name: "onboarding_prompts");

            migrationBuilder.DropTable(
                name: "outgoing_webhook_deliveries");

            migrationBuilder.DropTable(
                name: "profile_decorations");

            migrationBuilder.DropTable(
                name: "scheduled_messages");

            migrationBuilder.DropTable(
                name: "server_boosts");

            migrationBuilder.DropTable(
                name: "server_insight_snapshots");

            migrationBuilder.DropTable(
                name: "server_templates");

            migrationBuilder.DropTable(
                name: "slash_commands");

            migrationBuilder.DropTable(
                name: "soundboard_sounds");

            migrationBuilder.DropTable(
                name: "stage_speakers");

            migrationBuilder.DropTable(
                name: "user_activities");

            migrationBuilder.DropTable(
                name: "user_notes");

            migrationBuilder.DropTable(
                name: "welcome_screen_channels");

            migrationBuilder.DropTable(
                name: "app_listings");

            migrationBuilder.DropTable(
                name: "federation_follows");

            migrationBuilder.DropTable(
                name: "onboarding_configs");

            migrationBuilder.DropTable(
                name: "outgoing_webhooks");

            migrationBuilder.DropTable(
                name: "stage_sessions");

            migrationBuilder.DropTable(
                name: "welcome_screens");

            migrationBuilder.DropIndex(
                name: "IX_servers_VanitySlug",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "ScheduledDeletionAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "BoostCount",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "BoostLevel",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "VanitySlug",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "IsSuper",
                table: "reactions");
        }
    }
}
