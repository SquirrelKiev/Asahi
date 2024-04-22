using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.SqliteMigrations
{
    /// <inheritdoc />
    public partial class CachedHighlightsMessageChannelColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "OriginalMessageChannelId",
                table: "CachedHighlightedMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalMessageChannelId",
                table: "CachedHighlightedMessages");
        }
    }
}
