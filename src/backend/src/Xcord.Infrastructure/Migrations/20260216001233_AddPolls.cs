using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmbedsProcessed",
                table: "messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

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
                    MuteUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "polls",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    Question = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AllowMultipleAnswers = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    ScheduledStartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledEndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                name: "event_rsvps",
                columns: table => new
                {
                    EventId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "poll_votes",
                columns: table => new
                {
                    PollOptionId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                unique: true);

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
                name: "IX_sticker_packs_ServerId",
                table: "sticker_packs",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_stickers_StickerPackId",
                table: "stickers",
                column: "StickerPackId");

            migrationBuilder.CreateIndex(
                name: "IX_user_blocks_BlockedId",
                table: "user_blocks",
                column: "BlockedId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "custom_emojis");

            migrationBuilder.DropTable(
                name: "embeds");

            migrationBuilder.DropTable(
                name: "event_rsvps");

            migrationBuilder.DropTable(
                name: "forum_post_tags");

            migrationBuilder.DropTable(
                name: "friendships");

            migrationBuilder.DropTable(
                name: "notification_settings");

            migrationBuilder.DropTable(
                name: "poll_votes");

            migrationBuilder.DropTable(
                name: "read_states");

            migrationBuilder.DropTable(
                name: "stickers");

            migrationBuilder.DropTable(
                name: "user_blocks");

            migrationBuilder.DropTable(
                name: "scheduled_events");

            migrationBuilder.DropTable(
                name: "forum_tags");

            migrationBuilder.DropTable(
                name: "poll_options");

            migrationBuilder.DropTable(
                name: "sticker_packs");

            migrationBuilder.DropTable(
                name: "polls");

            migrationBuilder.DropColumn(
                name: "EmbedsProcessed",
                table: "messages");
        }
    }
}
