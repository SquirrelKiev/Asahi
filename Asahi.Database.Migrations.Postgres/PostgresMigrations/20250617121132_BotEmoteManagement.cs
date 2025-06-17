#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Asahi.Migrations.Postgres
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
                    EmoteKey = table.Column<string>(type: "text", nullable: false),
                    EmoteId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsAnimated = table.Column<bool>(type: "boolean", nullable: false),
                    EmoteDataIdentifier = table.Column<byte[]>(type: "bytea", nullable: false)
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
