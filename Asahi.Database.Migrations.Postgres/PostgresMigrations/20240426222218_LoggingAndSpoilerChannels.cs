using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.PostgresMigrations
{
    /// <inheritdoc />
    public partial class LoggingAndSpoilerChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoggingChannelOverride",
                columns: table => new
                {
                    OverriddenChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    HighlightBoardGuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    HighlightBoardName = table.Column<string>(type: "character varying(32)", nullable: false),
                    LoggingChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoggingChannelOverride", x => new { x.OverriddenChannelId, x.HighlightBoardGuildId, x.HighlightBoardName });
                    table.ForeignKey(
                        name: "FK_LoggingChannelOverride_HighlightBoards_HighlightBoardGuildI~",
                        columns: x => new { x.HighlightBoardGuildId, x.HighlightBoardName },
                        principalTable: "HighlightBoards",
                        principalColumns: new[] { "GuildId", "Name" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpoilerChannel",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    HighlightBoardGuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    HighlightBoardName = table.Column<string>(type: "character varying(32)", nullable: false),
                    SpoilerContext = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpoilerChannel", x => new { x.ChannelId, x.HighlightBoardGuildId, x.HighlightBoardName });
                    table.ForeignKey(
                        name: "FK_SpoilerChannel_HighlightBoards_HighlightBoardGuildId_Highli~",
                        columns: x => new { x.HighlightBoardGuildId, x.HighlightBoardName },
                        principalTable: "HighlightBoards",
                        principalColumns: new[] { "GuildId", "Name" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoggingChannelOverride_HighlightBoardGuildId_HighlightBoard~",
                table: "LoggingChannelOverride",
                columns: new[] { "HighlightBoardGuildId", "HighlightBoardName" });

            migrationBuilder.CreateIndex(
                name: "IX_SpoilerChannel_HighlightBoardGuildId_HighlightBoardName",
                table: "SpoilerChannel",
                columns: new[] { "HighlightBoardGuildId", "HighlightBoardName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoggingChannelOverride");

            migrationBuilder.DropTable(
                name: "SpoilerChannel");
        }
    }
}
