using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixColumnNamingAndAddNewTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_roles_ServerId_IsEveryone",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "IX_custom_emojis_ServerId_Name",
                table: "custom_emojis");

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    ActorId = table.Column<long>(type: "bigint", nullable: false),
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
                        name: "FK_audit_logs_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    table.ForeignKey(
                        name: "FK_automod_rules_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    ModeratorId = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DeleteMessageDays = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bans_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bans_users_ModeratorId",
                        column: x => x.ModeratorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bans_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
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
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "timeouts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<long>(type: "bigint", nullable: false),
                    ModeratorId = table.Column<long>(type: "bigint", nullable: false),
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
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_timeouts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_roles_ServerId_IsEveryone",
                table: "roles",
                columns: new[] { "ServerId", "IsEveryone" },
                filter: "\"IsEveryone\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_custom_emojis_ServerId_Name",
                table: "custom_emojis",
                columns: new[] { "ServerId", "Name" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

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
                columns: new[] { "ServerId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_bans_UserId",
                table: "bans",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "automod_rules");

            migrationBuilder.DropTable(
                name: "bans");

            migrationBuilder.DropTable(
                name: "calls");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "timeouts");

            migrationBuilder.DropIndex(
                name: "IX_roles_ServerId_IsEveryone",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "IX_custom_emojis_ServerId_Name",
                table: "custom_emojis");

            migrationBuilder.CreateIndex(
                name: "IX_roles_ServerId_IsEveryone",
                table: "roles",
                columns: new[] { "ServerId", "IsEveryone" },
                filter: "\"IsEveryone\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_custom_emojis_ServerId_Name",
                table: "custom_emojis",
                columns: new[] { "ServerId", "Name" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }
    }
}
