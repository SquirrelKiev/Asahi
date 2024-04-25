using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.SqliteMigrations
{
    /// <inheritdoc />
    public partial class BotDbConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BotWideConfig",
                columns: table => new
                {
                    DumbKey = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShouldHaveActivity = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityType = table.Column<int>(type: "INTEGER", nullable: false),
                    BotActivity = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ActivityStreamingUrl = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
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
                    Comment = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BotWideConfigDumbKey = table.Column<bool>(type: "INTEGER", nullable: true)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrustedId");

            migrationBuilder.DropTable(
                name: "BotWideConfig");
        }
    }
}
