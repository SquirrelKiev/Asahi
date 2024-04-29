using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.SqliteMigrations
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
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SpoilerBotAutoDeleteOriginal",
                table: "GuildConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SpoilerReactionEmote",
                table: "GuildConfigs",
                type: "TEXT",
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
