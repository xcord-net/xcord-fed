using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReactionsAndEdits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    S3Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    ThumbnailS3Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsConfirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                name: "message_edits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    PreviousContent = table.Column<string>(type: "text", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "reactions",
                columns: table => new
                {
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Emoji = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                name: "thread_members",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ThreadId = table.Column<long>(type: "bigint", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_attachments_MessageId",
                table: "attachments",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_message_edits_MessageId",
                table: "message_edits",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_reactions_MessageId",
                table: "reactions",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_reactions_UserId",
                table: "reactions",
                column: "UserId");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "message_edits");

            migrationBuilder.DropTable(
                name: "reactions");

            migrationBuilder.DropTable(
                name: "thread_members");

            migrationBuilder.DropTable(
                name: "threads");
        }
    }
}
