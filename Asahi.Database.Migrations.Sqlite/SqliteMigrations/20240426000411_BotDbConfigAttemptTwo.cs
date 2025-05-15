#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Asahi.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class BotDbConfigAttemptTwo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BotWideConfig",
                columns: table => new
                {
                    BotId = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShouldHaveActivity = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityType = table.Column<int>(type: "INTEGER", nullable: false),
                    BotActivity = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ActivityStreamingUrl = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotWideConfig", x => x.BotId);
                });

            migrationBuilder.CreateTable(
                name: "TrustedIds",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PermissionFlags = table.Column<int>(type: "INTEGER", nullable: false),
                    BotWideConfigBotId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustedIds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrustedIds_BotWideConfig_BotWideConfigBotId",
                        column: x => x.BotWideConfigBotId,
                        principalTable: "BotWideConfig",
                        principalColumn: "BotId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrustedIds_BotWideConfigBotId",
                table: "TrustedIds",
                column: "BotWideConfigBotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrustedIds");

            migrationBuilder.DropTable(
                name: "BotWideConfig");
        }
    }
}
