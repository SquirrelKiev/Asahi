using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class HighlightsRounding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "RoundingThreshold",
                table: "HighlightThreshold",
                type: "real",
                nullable: false,
                defaultValue: 0.4f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoundingThreshold",
                table: "HighlightThreshold");
        }
    }
}
