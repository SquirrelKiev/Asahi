using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.PostgresMigrations
{
    /// <inheritdoc />
    public partial class MultipleActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BotActivity",
                table: "BotWideConfig");

            migrationBuilder.AddColumn<string[]>(
                name: "BotActivities",
                table: "BotWideConfig",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BotActivities",
                table: "BotWideConfig");

            migrationBuilder.AddColumn<string>(
                name: "BotActivity",
                table: "BotWideConfig",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");
        }
    }
}
