#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Asahi.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class BotDbConfigReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrustedId");

            migrationBuilder.DropTable(
                name: "BotWideConfig");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BotWideConfig",
                columns: table => new
                {
                    DumbKey = table.Column<bool>(type: "INTEGER", nullable: false),
                    ActivityStreamingUrl = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ActivityType = table.Column<int>(type: "INTEGER", nullable: false),
                    BotActivity = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ShouldHaveActivity = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserStatus = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotWideConfig", x => x.DumbKey);
                });

            migrationBuilder.CreateTable(
                name: "TrustedId",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BotWideConfigDumbKey = table.Column<bool>(type: "INTEGER", nullable: true),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustedId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrustedId_BotWideConfig_BotWideConfigDumbKey",
                        column: x => x.BotWideConfigDumbKey,
                        principalTable: "BotWideConfig",
                        principalColumn: "DumbKey");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrustedId_BotWideConfigDumbKey",
                table: "TrustedId",
                column: "BotWideConfigDumbKey");
        }
    }
}
