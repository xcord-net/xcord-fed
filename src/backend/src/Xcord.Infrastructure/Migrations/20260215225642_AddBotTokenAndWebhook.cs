using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBotTokenAndWebhook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bot_tokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Permissions = table.Column<long>(type: "bigint", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_bot_tokens_TokenHash",
                table: "bot_tokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_bot_tokens_UserId",
                table: "bot_tokens",
                column: "UserId");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bot_tokens");

            migrationBuilder.DropTable(
                name: "webhooks");
        }
    }
}
