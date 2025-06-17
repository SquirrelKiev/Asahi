#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Asahi.Migrations.Sqlite.Migrations
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

            migrationBuilder.AddColumn<string>(
                name: "BotActivities",
                table: "BotWideConfig",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
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
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");
        }
    }
}
