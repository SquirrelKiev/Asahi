#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Asahi.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class HighlightsMuteRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "HighlightsMuteRole",
                table: "HighlightBoards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HighlightsMuteRole",
                table: "HighlightBoards");
        }
    }
}
