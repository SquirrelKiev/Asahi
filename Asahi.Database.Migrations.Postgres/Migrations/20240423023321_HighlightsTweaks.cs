using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class HighlightsTweaks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequireSendMessagePermissionInChannel",
                table: "HighlightBoards");

            migrationBuilder.AddColumn<int>(
                name: "HighActivityMessageLookBack",
                table: "HighlightThreshold",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HighActivityMessageMaxAgeSeconds",
                table: "HighlightThreshold",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "HighActivityMultiplier",
                table: "HighlightThreshold",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "UniqueUserDecayDelaySeconds",
                table: "HighlightThreshold",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HighActivityMessageLookBack",
                table: "HighlightThreshold");

            migrationBuilder.DropColumn(
                name: "HighActivityMessageMaxAgeSeconds",
                table: "HighlightThreshold");

            migrationBuilder.DropColumn(
                name: "HighActivityMultiplier",
                table: "HighlightThreshold");

            migrationBuilder.DropColumn(
                name: "UniqueUserDecayDelaySeconds",
                table: "HighlightThreshold");

            migrationBuilder.AddColumn<bool>(
                name: "RequireSendMessagePermissionInChannel",
                table: "HighlightBoards",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
