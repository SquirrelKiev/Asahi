#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Asahi.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class WelcomeModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShouldSendWelcomeMessage",
                table: "GuildConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<ulong>(
                name: "WelcomeMessageChannelId",
                table: "GuildConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<string>(
                name: "WelcomeMessageJson",
                table: "GuildConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShouldSendWelcomeMessage",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "WelcomeMessageChannelId",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "WelcomeMessageJson",
                table: "GuildConfigs");
        }
    }
}
