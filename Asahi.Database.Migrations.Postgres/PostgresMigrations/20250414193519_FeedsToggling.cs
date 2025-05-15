using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.PostgresMigrations
{
    /// <inheritdoc />
    public partial class FeedsToggling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisabledReason",
                table: "RssFeedListeners",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "RssFeedListeners",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ForcedDisable",
                table: "RssFeedListeners",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisabledReason",
                table: "RssFeedListeners");

            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "RssFeedListeners");

            migrationBuilder.DropColumn(
                name: "ForcedDisable",
                table: "RssFeedListeners");
        }
    }
}
