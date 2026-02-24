using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SoftDeleteUniqueIndexFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_EmailHash",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_Username",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_user_notes_AuthorId_TargetUserId",
                table: "user_notes");

            migrationBuilder.DropIndex(
                name: "IX_friendships_SenderId_ReceiverId",
                table: "friendships");

            migrationBuilder.DropIndex(
                name: "IX_federation_follows_LocalChannelId_RemoteInstanceUrl_RemoteC~",
                table: "federation_follows");

            migrationBuilder.DropIndex(
                name: "IX_bans_ServerId_UserId",
                table: "bans");

            migrationBuilder.CreateIndex(
                name: "IX_users_EmailHash",
                table: "users",
                column: "EmailHash",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "Username",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_notes_AuthorId_TargetUserId",
                table: "user_notes",
                columns: new[] { "AuthorId", "TargetUserId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_friendships_SenderId_ReceiverId",
                table: "friendships",
                columns: new[] { "SenderId", "ReceiverId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_federation_follows_LocalChannelId_RemoteInstanceUrl_RemoteC~",
                table: "federation_follows",
                columns: new[] { "LocalChannelId", "RemoteInstanceUrl", "RemoteChannelId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_bans_ServerId_UserId",
                table: "bans",
                columns: new[] { "ServerId", "UserId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_EmailHash",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_Username",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_user_notes_AuthorId_TargetUserId",
                table: "user_notes");

            migrationBuilder.DropIndex(
                name: "IX_friendships_SenderId_ReceiverId",
                table: "friendships");

            migrationBuilder.DropIndex(
                name: "IX_federation_follows_LocalChannelId_RemoteInstanceUrl_RemoteC~",
                table: "federation_follows");

            migrationBuilder.DropIndex(
                name: "IX_bans_ServerId_UserId",
                table: "bans");

            migrationBuilder.CreateIndex(
                name: "IX_users_EmailHash",
                table: "users",
                column: "EmailHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_notes_AuthorId_TargetUserId",
                table: "user_notes",
                columns: new[] { "AuthorId", "TargetUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_friendships_SenderId_ReceiverId",
                table: "friendships",
                columns: new[] { "SenderId", "ReceiverId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_federation_follows_LocalChannelId_RemoteInstanceUrl_RemoteC~",
                table: "federation_follows",
                columns: new[] { "LocalChannelId", "RemoteInstanceUrl", "RemoteChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bans_ServerId_UserId",
                table: "bans",
                columns: new[] { "ServerId", "UserId" },
                unique: true);
        }
    }
}
