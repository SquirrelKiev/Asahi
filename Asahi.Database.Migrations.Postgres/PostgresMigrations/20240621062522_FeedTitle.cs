using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.PostgresMigrations
{
    /// <inheritdoc />
    public partial class FeedTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FeedTitle",
                table: "RssFeedListeners",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeedTitle",
                table: "RssFeedListeners");
        }
    }
}
