using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagePinnedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_messages_ConversationId_CreatedAt",
                table: "messages");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PinnedAt",
                table: "messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_messages_ConversationId_CreatedAt",
                table: "messages",
                columns: new[] { "ConversationId", "CreatedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_messages_ConversationId_CreatedAt",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "PinnedAt",
                table: "messages");

            migrationBuilder.CreateIndex(
                name: "IX_messages_ConversationId_CreatedAt",
                table: "messages",
                columns: new[] { "ConversationId", "CreatedAt" });
        }
    }
}
