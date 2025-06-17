#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Asahi.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class BotEmoteManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InternalCustomEmoteTracking",
                columns: table => new
                {
                    EmoteKey = table.Column<string>(type: "TEXT", nullable: false),
                    EmoteId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    IsAnimated = table.Column<bool>(type: "INTEGER", nullable: false),
                    EmoteDataIdentifier = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalCustomEmoteTracking", x => x.EmoteKey);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InternalCustomEmoteTracking");
        }
    }
}
