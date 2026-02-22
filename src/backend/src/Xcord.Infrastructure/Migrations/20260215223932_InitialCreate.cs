using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Email = table.Column<byte[]>(type: "bytea", nullable: false),
                    EmailHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Bio = table.Column<string>(type: "character varying(190)", maxLength: 190, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CustomStatus = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsBot = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
