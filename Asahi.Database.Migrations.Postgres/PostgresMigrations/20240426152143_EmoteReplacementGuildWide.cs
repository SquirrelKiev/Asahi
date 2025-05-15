using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.PostgresMigrations
{
    /// <inheritdoc />
    public partial class EmoteReplacementGuildWide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmoteAliases",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EmoteName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EmoteReplacement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmoteAliases", x => new { x.GuildId, x.EmoteName });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmoteAliases");
        }
    }
}
