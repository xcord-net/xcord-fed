using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServerMembershipInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_servers_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invites", x => x.Code);
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
                name: "server_members",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    Nickname = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ServerAvatarUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_invites_CreatedByUserId",
                table: "invites",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_invites_ServerId",
                table: "invites",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_server_members_ServerId",
                table: "server_members",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_servers_OwnerId",
                table: "servers",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invites");

            migrationBuilder.DropTable(
                name: "server_members");

            migrationBuilder.DropTable(
                name: "servers");
        }
    }
}
