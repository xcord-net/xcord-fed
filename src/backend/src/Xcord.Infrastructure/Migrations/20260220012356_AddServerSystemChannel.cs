using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xcord.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServerSystemChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SystemChannelId",
                table: "servers",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_servers_SystemChannelId",
                table: "servers",
                column: "SystemChannelId");

            migrationBuilder.AddForeignKey(
                name: "FK_servers_channels_SystemChannelId",
                table: "servers",
                column: "SystemChannelId",
                principalTable: "channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_servers_channels_SystemChannelId",
                table: "servers");

            migrationBuilder.DropIndex(
                name: "IX_servers_SystemChannelId",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "SystemChannelId",
                table: "servers");
        }
    }
}
