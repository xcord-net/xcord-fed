using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagesAndMentions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorId = table.Column<long>(type: "bigint", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    ReplyToId = table.Column<long>(type: "bigint", nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                name: "mentions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    MentionedUserId = table.Column<long>(type: "bigint", nullable: true),
                    MentionedRoleId = table.Column<long>(type: "bigint", nullable: true),
                    IsEveryone = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mentions_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mentions_roles_MentionedRoleId",
                        column: x => x.MentionedRoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mentions_users_MentionedUserId",
                        column: x => x.MentionedUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mentions_MentionedRoleId",
                table: "mentions",
                column: "MentionedRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_mentions_MentionedUserId",
                table: "mentions",
                column: "MentionedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_mentions_MessageId",
                table: "mentions",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_messages_AuthorId",
                table: "messages",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_messages_ConversationId",
                table: "messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_messages_ReplyToId",
                table: "messages",
                column: "ReplyToId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mentions");

            migrationBuilder.DropTable(
                name: "messages");
        }
    }
}
