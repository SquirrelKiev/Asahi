using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.PostgresMigrations
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
                    DumbKey = table.Column<bool>(type: "boolean", nullable: false),
                    ActivityStreamingUrl = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActivityType = table.Column<int>(type: "integer", nullable: false),
                    BotActivity = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ShouldHaveActivity = table.Column<bool>(type: "boolean", nullable: false),
                    UserStatus = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotWideConfig", x => x.DumbKey);
                });

            migrationBuilder.CreateTable(
                name: "TrustedId",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    BotWideConfigDumbKey = table.Column<bool>(type: "boolean", nullable: true),
                    Comment = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
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
