using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    Permissions = table.Column<long>(type: "bigint", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsEveryone = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_roles_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "member_roles",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_roles", x => new { x.UserId, x.ServerId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_member_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_member_roles_server_members_UserId_ServerId",
                        columns: x => new { x.UserId, x.ServerId },
                        principalTable: "server_members",
                        principalColumns: new[] { "UserId", "ServerId" },
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_member_roles_RoleId",
                table: "member_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_roles_ServerId",
                table: "roles",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_roles_ServerId_IsEveryone",
                table: "roles",
                columns: new[] { "ServerId", "IsEveryone" },
                filter: "\"IsEveryone\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_permission_overrides");

            migrationBuilder.DropTable(
                name: "member_roles");

            migrationBuilder.DropTable(
                name: "roles");
        }
    }
}
