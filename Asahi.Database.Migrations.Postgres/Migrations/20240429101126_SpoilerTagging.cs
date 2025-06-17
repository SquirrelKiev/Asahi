using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class SpoilerTagging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SpoilerBotAutoDeleteContextSetting",
                table: "GuildConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SpoilerBotAutoDeleteOriginal",
                table: "GuildConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SpoilerReactionEmote",
                table: "GuildConfigs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpoilerBotAutoDeleteContextSetting",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "SpoilerBotAutoDeleteOriginal",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "SpoilerReactionEmote",
                table: "GuildConfigs");
        }
    }
}
