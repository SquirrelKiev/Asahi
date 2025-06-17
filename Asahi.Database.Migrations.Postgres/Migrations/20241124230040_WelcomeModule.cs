using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.Postgres.Migrations
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
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "WelcomeMessageChannelId",
                table: "GuildConfigs",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "WelcomeMessageJson",
                table: "GuildConfigs",
                type: "text",
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
