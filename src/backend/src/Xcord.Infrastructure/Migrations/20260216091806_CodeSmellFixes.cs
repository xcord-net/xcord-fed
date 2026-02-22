using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CodeSmellFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_users_ActorId",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_bans_users_ModeratorId",
                table: "bans");

            migrationBuilder.DropForeignKey(
                name: "FK_timeouts_users_ModeratorId",
                table: "timeouts");

            migrationBuilder.DropIndex(
                name: "IX_messages_ConversationId",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "IX_bans_ServerId_UserId",
                table: "bans");

            migrationBuilder.AlterColumn<long>(
                name: "ModeratorId",
                table: "timeouts",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "server_members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptAt",
                table: "outbox_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "invites",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "bot_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "ModeratorId",
                table: "bans",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "ActorId",
                table: "audit_logs",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateIndex(
                name: "IX_roles_ServerId_Position",
                table: "roles",
                columns: new[] { "ServerId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_ConversationId_CreatedAt",
                table: "messages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_invites_ExpiresAt",
                table: "invites",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_bans_ServerId_UserId",
                table: "bans",
                columns: new[] { "ServerId", "UserId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_users_ActorId",
                table: "audit_logs",
                column: "ActorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_bans_users_ModeratorId",
                table: "bans",
                column: "ModeratorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_timeouts_users_ModeratorId",
                table: "timeouts",
                column: "ModeratorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_users_ActorId",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_bans_users_ModeratorId",
                table: "bans");

            migrationBuilder.DropForeignKey(
                name: "FK_timeouts_users_ModeratorId",
                table: "timeouts");

            migrationBuilder.DropIndex(
                name: "IX_roles_ServerId_Position",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "IX_messages_ConversationId_CreatedAt",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "IX_invites_ExpiresAt",
                table: "invites");

            migrationBuilder.DropIndex(
                name: "IX_bans_ServerId_UserId",
                table: "bans");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "server_members");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "outbox_events");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "invites");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "bot_tokens");

            migrationBuilder.AlterColumn<long>(
                name: "ModeratorId",
                table: "timeouts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "ModeratorId",
                table: "bans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "ActorId",
                table: "audit_logs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_messages_ConversationId",
                table: "messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_bans_ServerId_UserId",
                table: "bans",
                columns: new[] { "ServerId", "UserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_users_ActorId",
                table: "audit_logs",
                column: "ActorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_bans_users_ModeratorId",
                table: "bans",
                column: "ModeratorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_timeouts_users_ModeratorId",
                table: "timeouts",
                column: "ModeratorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
