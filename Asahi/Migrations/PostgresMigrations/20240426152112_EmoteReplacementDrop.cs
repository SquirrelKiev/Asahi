using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.PostgresMigrations
{
    /// <inheritdoc />
    public partial class EmoteReplacementDrop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmoteAlias");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmoteAlias",
                columns: table => new
                {
                    EmoteName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HighlightBoardGuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    HighlightBoardName = table.Column<string>(type: "character varying(32)", nullable: false),
                    EmoteReplacement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmoteAlias", x => new { x.EmoteName, x.HighlightBoardGuildId, x.HighlightBoardName });
                    table.ForeignKey(
                        name: "FK_EmoteAlias_HighlightBoards_HighlightBoardGuildId_HighlightB~",
                        columns: x => new { x.HighlightBoardGuildId, x.HighlightBoardName },
                        principalTable: "HighlightBoards",
                        principalColumns: new[] { "GuildId", "Name" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmoteAlias_HighlightBoardGuildId_HighlightBoardName",
                table: "EmoteAlias",
                columns: new[] { "HighlightBoardGuildId", "HighlightBoardName" });
        }
    }
}
