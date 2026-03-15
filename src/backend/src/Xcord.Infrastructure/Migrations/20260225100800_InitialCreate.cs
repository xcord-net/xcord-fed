using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,");

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Email = table.Column<byte[]>(type: "bytea", nullable: false),
                    EmailHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Bio = table.Column<string>(type: "character varying(190)", maxLength: 190, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CustomStatus = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsBot = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TwoFactorFailureCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TwoFactorLockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MuteAll = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScheduledDeletionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bot_tokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Roles = table.Column<long>(type: "bigint", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InteractionEndpointUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    InteractionSigningKey = table.Column<byte[]>(type: "bytea", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bot_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dm_channels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    IsGroup = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OwnerId = table.Column<long>(type: "bigint", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dm_channels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dm_channels_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dm_channels_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "email_confirmation_tokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_confirmation_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_confirmation_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "friendships",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    SenderId = table.Column<long>(type: "bigint", nullable: false),
                    ReceiverId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_friendships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_friendships_users_ReceiverId",
                        column: x => x.ReceiverId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_friendships_users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorId = table.Column<long>(type: "bigint", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    ReplyToId = table.Column<long>(type: "bigint", nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PinnedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EmbedsProcessed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EditedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_messages_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_messages_messages_ReplyToId",
                        column: x => x.ReplyToId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_messages_users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_password_reset_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_messages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorId = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                name: "two_factor_backup_codes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_two_factor_backup_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_two_factor_backup_codes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "two_factor_codes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_two_factor_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_two_factor_codes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
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
                name: "user_blocks",
                columns: table => new
                {
                    BlockerId = table.Column<long>(type: "bigint", nullable: false),
                    BlockedId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_blocks", x => new { x.BlockerId, x.BlockedId });
                    table.ForeignKey(
                        name: "FK_user_blocks_users_BlockedId",
                        column: x => x.BlockedId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_blocks_users_BlockerId",
                        column: x => x.BlockerId,
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
                name: "webhooks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_webhooks_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

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
                name: "calls",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    DmChannelId = table.Column<long>(type: "bigint", nullable: false),
                    CallerId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_calls_dm_channels_DmChannelId",
                        column: x => x.DmChannelId,
                        principalTable: "dm_channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_calls_users_CallerId",
                        column: x => x.CallerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dm_channel_members",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    DmChannelId = table.Column<long>(type: "bigint", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dm_channel_members", x => new { x.UserId, x.DmChannelId });
                    table.ForeignKey(
                        name: "FK_dm_channel_members_dm_channels_DmChannelId",
                        column: x => x.DmChannelId,
                        principalTable: "dm_channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dm_channel_members_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: true),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    S3Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    ThumbnailS3Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsConfirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attachments_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "embeds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ImageS3Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SiteName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MessageId1 = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_embeds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_embeds_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_embeds_messages_MessageId1",
                        column: x => x.MessageId1,
                        principalTable: "messages",
                        principalColumn: "Id");
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
                name: "message_edits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    PreviousContent = table.Column<string>(type: "text", nullable: false),
                    EditedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_edits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_message_edits_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "polls",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    Question = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AllowMultipleAnswers = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_polls_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reactions",
                columns: table => new
                {
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Emoji = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reactions", x => new { x.MessageId, x.UserId, x.Emoji });
                    table.ForeignKey(
                        name: "FK_reactions_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reactions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "read_states",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    LastReadMessageId = table.Column<long>(type: "bigint", nullable: true),
                    UnreadCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MentionCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_read_states", x => new { x.UserId, x.ConversationId });
                    table.ForeignKey(
                        name: "FK_read_states_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_read_states_messages_LastReadMessageId",
                        column: x => x.LastReadMessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_read_states_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
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
                name: "poll_options",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PollId = table.Column<long>(type: "bigint", nullable: false),
                    Text = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EmojiUnicode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    VoteCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poll_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_poll_options_polls_PollId",
                        column: x => x.PollId,
                        principalTable: "polls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "poll_votes",
                columns: table => new
                {
                    PollOptionId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poll_votes", x => new { x.PollOptionId, x.UserId });
                    table.ForeignKey(
                        name: "FK_poll_votes_poll_options_PollOptionId",
                        column: x => x.PollOptionId,
                        principalTable: "poll_options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_poll_votes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    ActorId = table.Column<long>(type: "bigint", nullable: true),
                    ActionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TargetId = table.Column<long>(type: "bigint", nullable: true),
                    Changes = table.Column<string>(type: "jsonb", nullable: true),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "automod_rules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    TriggerType = table.Column<int>(type: "integer", nullable: false),
                    TriggerConfig = table.Column<string>(type: "jsonb", nullable: false),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    ActionConfig = table.Column<string>(type: "jsonb", nullable: true),
                    ExemptRoleIds = table.Column<string>(type: "text", nullable: true),
                    ExemptChannelIds = table.Column<string>(type: "text", nullable: true),
                    ExemptBots = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automod_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    ModeratorId = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DeleteMessageDays = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bans_users_ModeratorId",
                        column: x => x.ModeratorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_bans_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "channel_permission_overrides",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<long>(type: "bigint", nullable: false),
                    Allow = table.Column<long>(type: "bigint", nullable: false),
                    Deny = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channel_permission_overrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "channels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    CategoryId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Topic = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    SlowModeSeconds = table.Column<int>(type: "integer", nullable: true),
                    IsNsfw = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DefaultSortOrder = table.Column<int>(type: "integer", nullable: true),
                    RequireTag = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DefaultAutoArchiveDuration = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_channels_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_channels_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "conversations",
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
                name: "forum_tags",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EmojiUnicode = table.Column<string>(type: "text", nullable: true),
                    EmojiId = table.Column<long>(type: "bigint", nullable: true),
                    IsModerated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_forum_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_forum_tags_channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "servers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IconUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    BannerUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    OwnerId = table.Column<long>(type: "bigint", nullable: false),
                    MemberCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PreferredLocale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    VanitySlug = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    BoostLevel = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    BoostCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SystemChannelId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_servers_channels_SystemChannelId",
                        column: x => x.SystemChannelId,
                        principalTable: "channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_servers_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "threads",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    ParentMessageId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AutoArchiveDurationMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 1440),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MessageCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_threads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_threads_channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_threads_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_threads_messages_ParentMessageId",
                        column: x => x.ParentMessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "voice_states",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    IsMuted = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeafened = table.Column<bool>(type: "boolean", nullable: false),
                    IsStreaming = table.Column<bool>(type: "boolean", nullable: false),
                    IsServerMuted = table.Column<bool>(type: "boolean", nullable: false),
                    IsServerDeafened = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voice_states", x => new { x.UserId, x.ChannelId });
                    table.ForeignKey(
                        name: "FK_voice_states_channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_voice_states_users_UserId",
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
                name: "custom_emojis",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    S3Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsAnimated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatorId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_emojis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_custom_emojis_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_custom_emojis_users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    Roles = table.Column<long>(type: "bigint", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsEveryone = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LimitsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_groups_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_settings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: true),
                    ChannelId = table.Column<long>(type: "bigint", nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SuppressEveryone = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SuppressRoles = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MuteUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_settings_channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notification_settings_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notification_settings_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
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
                    TargetUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Secret = table.Column<byte[]>(type: "bytea", nullable: false),
                    EventTypesJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_outgoing_webhooks_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    ReporterId = table.Column<long>(type: "bigint", nullable: false),
                    ReportedUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReportedMessageId = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewedById = table.Column<long>(type: "bigint", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reports_messages_ReportedMessageId",
                        column: x => x.ReportedMessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_reports_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reports_users_ReportedUserId",
                        column: x => x.ReportedUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_reports_users_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reports_users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    CreatorId = table.Column<long>(type: "bigint", nullable: false),
                    ChannelId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ScheduledStartTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScheduledEndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    InterestedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    NotificationSent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scheduled_events_channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_scheduled_events_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_scheduled_events_users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "server_billing_configs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    StripeConnectedAccountId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RevenueSharePercent = table.Column<int>(type: "integer", nullable: false),
                    PayoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_server_billing_configs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_server_billing_configs_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
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
                name: "server_members",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    Nickname = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ServerAvatarUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_server_members", x => new { x.UserId, x.ServerId });
                    table.ForeignKey(
                        name: "FK_server_members_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_server_members_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
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
                    GroupData = table.Column<string>(type: "jsonb", nullable: false),
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
                name: "sticker_packs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sticker_packs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sticker_packs_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tiers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PriceMonthly = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    GroupIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tiers_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "timeouts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    ModeratorId = table.Column<long>(type: "bigint", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_timeouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_timeouts_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_timeouts_users_ModeratorId",
                        column: x => x.ModeratorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_timeouts_users_UserId",
                        column: x => x.UserId,
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
                name: "forum_post_tags",
                columns: table => new
                {
                    ThreadId = table.Column<long>(type: "bigint", nullable: false),
                    ForumTagId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_forum_post_tags", x => new { x.ThreadId, x.ForumTagId });
                    table.ForeignKey(
                        name: "FK_forum_post_tags_forum_tags_ForumTagId",
                        column: x => x.ForumTagId,
                        principalTable: "forum_tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_forum_post_tags_threads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "thread_members",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ThreadId = table.Column<long>(type: "bigint", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_thread_members", x => new { x.UserId, x.ThreadId });
                    table.ForeignKey(
                        name: "FK_thread_members_threads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_thread_members_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invites",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    MaxUses = table.Column<int>(type: "integer", nullable: true),
                    Uses = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GroupId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invites", x => x.Code);
                    table.ForeignKey(
                        name: "FK_invites_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_invites_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_invites_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "mentions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    MentionedUserId = table.Column<long>(type: "bigint", nullable: true),
                    MentionedGroupId = table.Column<long>(type: "bigint", nullable: true),
                    IsEveryone = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mentions_groups_MentionedGroupId",
                        column: x => x.MentionedGroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mentions_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mentions_users_MentionedUserId",
                        column: x => x.MentionedUserId,
                        principalTable: "users",
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
                    WebhookId = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastHttpStatus = table.Column<int>(type: "integer", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outgoing_webhook_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_outgoing_webhook_deliveries_outgoing_webhooks_WebhookId",
                        column: x => x.WebhookId,
                        principalTable: "outgoing_webhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_rsvps",
                columns: table => new
                {
                    EventId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_rsvps", x => new { x.EventId, x.UserId });
                    table.ForeignKey(
                        name: "FK_event_rsvps_scheduled_events_EventId",
                        column: x => x.EventId,
                        principalTable: "scheduled_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_rsvps_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "member_groups",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    GroupId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_groups", x => new { x.UserId, x.ServerId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_member_groups_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_member_groups_server_members_UserId_ServerId",
                        columns: x => new { x.UserId, x.ServerId },
                        principalTable: "server_members",
                        principalColumns: new[] { "UserId", "ServerId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stickers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    StickerPackId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Tags = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    S3Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stickers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stickers_sticker_packs_StickerPackId",
                        column: x => x.StickerPackId,
                        principalTable: "sticker_packs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "member_subscriptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    TierId = table.Column<long>(type: "bigint", nullable: false),
                    StripeSubscriptionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    StripeCustomerId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrentPeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_member_subscriptions_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_member_subscriptions_tiers_TierId",
                        column: x => x.TierId,
                        principalTable: "tiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_member_subscriptions_users_UserId",
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
                name: "IX_attachments_MessageId",
                table: "attachments",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_ActionType",
                table: "audit_logs",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_ActorId",
                table: "audit_logs",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CreatedAt",
                table: "audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_ServerId",
                table: "audit_logs",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_automod_rules_ServerId",
                table: "automod_rules",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_bans_ModeratorId",
                table: "bans",
                column: "ModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_bans_ServerId_UserId",
                table: "bans",
                columns: new[] { "ServerId", "UserId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_bans_UserId",
                table: "bans",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_bot_tokens_TokenHash",
                table: "bot_tokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_bot_tokens_UserId",
                table: "bot_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_calls_CallerId",
                table: "calls",
                column: "CallerId");

            migrationBuilder.CreateIndex(
                name: "IX_calls_DmChannelId",
                table: "calls",
                column: "DmChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_calls_DmChannelId_Status",
                table: "calls",
                columns: new[] { "DmChannelId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_calls_Status",
                table: "calls",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_categories_ServerId_Position",
                table: "categories",
                columns: new[] { "ServerId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_channel_permission_overrides_ChannelId",
                table: "channel_permission_overrides",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_channel_permission_overrides_ChannelId_TargetType_TargetId",
                table: "channel_permission_overrides",
                columns: new[] { "ChannelId", "TargetType", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_channels_CategoryId",
                table: "channels",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_channels_ConversationId",
                table: "channels",
                column: "ConversationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_channels_ServerId_Position",
                table: "channels",
                columns: new[] { "ServerId", "Position" });

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
                name: "IX_custom_emojis_CreatorId",
                table: "custom_emojis",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_custom_emojis_ServerId",
                table: "custom_emojis",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_custom_emojis_ServerId_Name",
                table: "custom_emojis",
                columns: new[] { "ServerId", "Name" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_dm_channel_members_DmChannelId",
                table: "dm_channel_members",
                column: "DmChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_dm_channels_ConversationId",
                table: "dm_channels",
                column: "ConversationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dm_channels_OwnerId",
                table: "dm_channels",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_email_confirmation_tokens_ExpiresAt",
                table: "email_confirmation_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_email_confirmation_tokens_UserId",
                table: "email_confirmation_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_embeds_MessageId",
                table: "embeds",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_embeds_MessageId1",
                table: "embeds",
                column: "MessageId1");

            migrationBuilder.CreateIndex(
                name: "IX_event_rsvps_UserId",
                table: "event_rsvps",
                column: "UserId");

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
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

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
                name: "IX_forum_post_tags_ForumTagId",
                table: "forum_post_tags",
                column: "ForumTagId");

            migrationBuilder.CreateIndex(
                name: "IX_forum_tags_ChannelId",
                table: "forum_tags",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_forum_tags_ChannelId_Position",
                table: "forum_tags",
                columns: new[] { "ChannelId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_friendships_ReceiverId",
                table: "friendships",
                column: "ReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_friendships_SenderId_ReceiverId",
                table: "friendships",
                columns: new[] { "SenderId", "ReceiverId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_groups_ServerId",
                table: "groups",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_groups_ServerId_IsEveryone",
                table: "groups",
                columns: new[] { "ServerId", "IsEveryone" },
                filter: "\"IsEveryone\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_groups_ServerId_Position",
                table: "groups",
                columns: new[] { "ServerId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_invites_CreatedByUserId",
                table: "invites",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_invites_ExpiresAt",
                table: "invites",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_invites_GroupId",
                table: "invites",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_invites_ServerId",
                table: "invites",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_member_groups_GroupId",
                table: "member_groups",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_member_subscriptions_ServerId",
                table: "member_subscriptions",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_member_subscriptions_StripeSubscriptionId",
                table: "member_subscriptions",
                column: "StripeSubscriptionId",
                filter: "\"StripeSubscriptionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_member_subscriptions_TierId",
                table: "member_subscriptions",
                column: "TierId");

            migrationBuilder.CreateIndex(
                name: "IX_member_subscriptions_UserId_ServerId",
                table: "member_subscriptions",
                columns: new[] { "UserId", "ServerId" },
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_mentions_MentionedGroupId",
                table: "mentions",
                column: "MentionedGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_mentions_MentionedUserId",
                table: "mentions",
                column: "MentionedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_mentions_MessageId",
                table: "mentions",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_message_components_MessageId",
                table: "message_components",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_message_edits_MessageId",
                table: "message_edits",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_messages_AuthorId",
                table: "messages",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_messages_ConversationId_CreatedAt",
                table: "messages",
                columns: new[] { "ConversationId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_messages_ReplyToId",
                table: "messages",
                column: "ReplyToId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_settings_ChannelId",
                table: "notification_settings",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_settings_ServerId",
                table: "notification_settings",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_settings_UserId_ServerId_ChannelId",
                table: "notification_settings",
                columns: new[] { "UserId", "ServerId", "ChannelId" },
                unique: true);

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
                name: "ix_outgoing_webhook_deliveries_created_at",
                table: "outgoing_webhook_deliveries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_outgoing_webhook_deliveries_status_next_attempt",
                table: "outgoing_webhook_deliveries",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "ix_outgoing_webhook_deliveries_webhook_id_status",
                table: "outgoing_webhook_deliveries",
                columns: new[] { "WebhookId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_outgoing_webhooks_CreatedByUserId",
                table: "outgoing_webhooks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_outgoing_webhooks_ServerId",
                table: "outgoing_webhooks",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_ExpiresAt",
                table: "password_reset_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_TokenHash",
                table: "password_reset_tokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_UserId",
                table: "password_reset_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_poll_options_PollId",
                table: "poll_options",
                column: "PollId");

            migrationBuilder.CreateIndex(
                name: "IX_poll_votes_PollOptionId",
                table: "poll_votes",
                column: "PollOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_poll_votes_UserId",
                table: "poll_votes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_polls_IsClosed_ExpiresAt",
                table: "polls",
                columns: new[] { "IsClosed", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_polls_MessageId",
                table: "polls",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reactions_MessageId",
                table: "reactions",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_reactions_UserId",
                table: "reactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_read_states_ConversationId",
                table: "read_states",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_read_states_LastReadMessageId",
                table: "read_states",
                column: "LastReadMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_read_states_UserId",
                table: "read_states",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_ExpiresAt",
                table: "refresh_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId",
                table: "refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_reports_ReportedMessageId",
                table: "reports",
                column: "ReportedMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_reports_ReportedUserId",
                table: "reports",
                column: "ReportedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_reports_ReporterId",
                table: "reports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_reports_ReviewedById",
                table: "reports",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_reports_ServerId",
                table: "reports",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_reports_Status",
                table: "reports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_events_ChannelId",
                table: "scheduled_events",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_events_CreatorId",
                table: "scheduled_events",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_events_ScheduledStartTime",
                table: "scheduled_events",
                column: "ScheduledStartTime");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_events_ServerId",
                table: "scheduled_events",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_events_ServerId_Status",
                table: "scheduled_events",
                columns: new[] { "ServerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_messages_AuthorId",
                table: "scheduled_messages",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_messages_ConversationId_ScheduledAt",
                table: "scheduled_messages",
                columns: new[] { "ConversationId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_messages_ScheduledAt_SentAt",
                table: "scheduled_messages",
                columns: new[] { "ScheduledAt", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_server_billing_configs_ServerId",
                table: "server_billing_configs",
                column: "ServerId",
                unique: true);

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
                name: "IX_server_members_ServerId",
                table: "server_members",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_server_templates_SourceServerId",
                table: "server_templates",
                column: "SourceServerId");

            migrationBuilder.CreateIndex(
                name: "IX_servers_OwnerId",
                table: "servers",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_servers_SystemChannelId",
                table: "servers",
                column: "SystemChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_servers_VanitySlug",
                table: "servers",
                column: "VanitySlug",
                unique: true,
                filter: "\"VanitySlug\" IS NOT NULL");

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
                name: "IX_sticker_packs_ServerId",
                table: "sticker_packs",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_stickers_StickerPackId",
                table: "stickers",
                column: "StickerPackId");

            migrationBuilder.CreateIndex(
                name: "IX_thread_members_ThreadId",
                table: "thread_members",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_threads_ChannelId",
                table: "threads",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_threads_ConversationId",
                table: "threads",
                column: "ConversationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_threads_IsArchived_LastActivityAt",
                table: "threads",
                columns: new[] { "IsArchived", "LastActivityAt" });

            migrationBuilder.CreateIndex(
                name: "IX_threads_ParentMessageId",
                table: "threads",
                column: "ParentMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_tiers_ServerId",
                table: "tiers",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_timeouts_ExpiresAt",
                table: "timeouts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_timeouts_ModeratorId",
                table: "timeouts",
                column: "ModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_timeouts_ServerId_UserId",
                table: "timeouts",
                columns: new[] { "ServerId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_timeouts_UserId",
                table: "timeouts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_two_factor_backup_codes_UserId",
                table: "two_factor_backup_codes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_two_factor_codes_ExpiresAt",
                table: "two_factor_codes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_two_factor_codes_UserId",
                table: "two_factor_codes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_activities_UserId",
                table: "user_activities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_blocks_BlockedId",
                table: "user_blocks",
                column: "BlockedId");

            migrationBuilder.CreateIndex(
                name: "IX_user_notes_AuthorId_TargetUserId",
                table: "user_notes",
                columns: new[] { "AuthorId", "TargetUserId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_notes_TargetUserId",
                table: "user_notes",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_EmailHash",
                table: "users",
                column: "EmailHash",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "Username",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_voice_states_ChannelId",
                table: "voice_states",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_webhooks_ChannelId",
                table: "webhooks",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_webhooks_CreatedByUserId",
                table: "webhooks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_webhooks_Token",
                table: "webhooks",
                column: "Token",
                unique: true);

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

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_servers_ServerId",
                table: "audit_logs",
                column: "ServerId",
                principalTable: "servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_automod_rules_servers_ServerId",
                table: "automod_rules",
                column: "ServerId",
                principalTable: "servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_bans_servers_ServerId",
                table: "bans",
                column: "ServerId",
                principalTable: "servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_categories_servers_ServerId",
                table: "categories",
                column: "ServerId",
                principalTable: "servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_channel_permission_overrides_channels_ChannelId",
                table: "channel_permission_overrides",
                column: "ChannelId",
                principalTable: "channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_channels_servers_ServerId",
                table: "channels",
                column: "ServerId",
                principalTable: "servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropForeignKey(
                name: "FK_servers_users_OwnerId",
                table: "servers");

            migrationBuilder.DropForeignKey(
                name: "FK_categories_servers_ServerId",
                table: "categories");

            migrationBuilder.DropForeignKey(
                name: "FK_channels_servers_ServerId",
                table: "channels");

            migrationBuilder.DropTable(
                name: "app_reviews");

            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "automod_rules");

            migrationBuilder.DropTable(
                name: "bans");

            migrationBuilder.DropTable(
                name: "calls");

            migrationBuilder.DropTable(
                name: "channel_permission_overrides");

            migrationBuilder.DropTable(
                name: "crosspost_subscriptions");

            migrationBuilder.DropTable(
                name: "custom_emojis");

            migrationBuilder.DropTable(
                name: "dm_channel_members");

            migrationBuilder.DropTable(
                name: "email_confirmation_tokens");

            migrationBuilder.DropTable(
                name: "embeds");

            migrationBuilder.DropTable(
                name: "event_rsvps");

            migrationBuilder.DropTable(
                name: "federation_messages");

            migrationBuilder.DropTable(
                name: "forum_post_tags");

            migrationBuilder.DropTable(
                name: "friendships");

            migrationBuilder.DropTable(
                name: "invites");

            migrationBuilder.DropTable(
                name: "member_groups");

            migrationBuilder.DropTable(
                name: "member_subscriptions");

            migrationBuilder.DropTable(
                name: "mentions");

            migrationBuilder.DropTable(
                name: "message_components");

            migrationBuilder.DropTable(
                name: "message_edits");

            migrationBuilder.DropTable(
                name: "notification_settings");

            migrationBuilder.DropTable(
                name: "onboarding_completions");

            migrationBuilder.DropTable(
                name: "onboarding_prompts");

            migrationBuilder.DropTable(
                name: "outgoing_webhook_deliveries");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "poll_votes");

            migrationBuilder.DropTable(
                name: "reactions");

            migrationBuilder.DropTable(
                name: "read_states");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "scheduled_messages");

            migrationBuilder.DropTable(
                name: "server_billing_configs");

            migrationBuilder.DropTable(
                name: "server_boosts");

            migrationBuilder.DropTable(
                name: "server_insight_snapshots");

            migrationBuilder.DropTable(
                name: "server_templates");

            migrationBuilder.DropTable(
                name: "slash_commands");

            migrationBuilder.DropTable(
                name: "stickers");

            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropTable(
                name: "thread_members");

            migrationBuilder.DropTable(
                name: "timeouts");

            migrationBuilder.DropTable(
                name: "two_factor_backup_codes");

            migrationBuilder.DropTable(
                name: "two_factor_codes");

            migrationBuilder.DropTable(
                name: "user_activities");

            migrationBuilder.DropTable(
                name: "user_blocks");

            migrationBuilder.DropTable(
                name: "user_notes");

            migrationBuilder.DropTable(
                name: "voice_states");

            migrationBuilder.DropTable(
                name: "webhooks");

            migrationBuilder.DropTable(
                name: "welcome_screen_channels");

            migrationBuilder.DropTable(
                name: "app_listings");

            migrationBuilder.DropTable(
                name: "dm_channels");

            migrationBuilder.DropTable(
                name: "scheduled_events");

            migrationBuilder.DropTable(
                name: "federation_follows");

            migrationBuilder.DropTable(
                name: "forum_tags");

            migrationBuilder.DropTable(
                name: "server_members");

            migrationBuilder.DropTable(
                name: "tiers");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropTable(
                name: "onboarding_configs");

            migrationBuilder.DropTable(
                name: "outgoing_webhooks");

            migrationBuilder.DropTable(
                name: "poll_options");

            migrationBuilder.DropTable(
                name: "sticker_packs");

            migrationBuilder.DropTable(
                name: "threads");

            migrationBuilder.DropTable(
                name: "welcome_screens");

            migrationBuilder.DropTable(
                name: "bot_tokens");

            migrationBuilder.DropTable(
                name: "polls");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "servers");

            migrationBuilder.DropTable(
                name: "channels");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "conversations");
        }
    }
}
