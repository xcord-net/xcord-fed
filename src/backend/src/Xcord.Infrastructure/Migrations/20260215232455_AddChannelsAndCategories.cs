using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelsAndCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    table.ForeignKey(
                        name: "FK_categories_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    table.ForeignKey(
                        name: "FK_channels_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_categories_ServerId_Position",
                table: "categories",
                columns: new[] { "ServerId", "Position" });

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

            migrationBuilder.AddForeignKey(
                name: "FK_channel_permission_overrides_channels_ChannelId",
                table: "channel_permission_overrides",
                column: "ChannelId",
                principalTable: "channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_channel_permission_overrides_channels_ChannelId",
                table: "channel_permission_overrides");

            migrationBuilder.DropTable(
                name: "channels");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "conversations");
        }
    }
}
