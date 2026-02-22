using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDmChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "dm_channel_members",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    DmChannelId = table.Column<long>(type: "bigint", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dm_channel_members");

            migrationBuilder.DropTable(
                name: "dm_channels");
        }
    }
}
