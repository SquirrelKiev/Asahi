using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Asahi.Migrations.PostgresMigrations
{
    /// <inheritdoc />
    public partial class BirthdayRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DefaultBirthdayConfigGuildId",
                table: "GuildConfigs",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultBirthdayConfigName",
                table: "GuildConfigs",
                type: "character varying(32)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BirthdayConfigs",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    BirthdayRole = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    AllowedRoles = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false),
                    DeniedRoles = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false),
                    EditWindowSeconds = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EmbedTitleText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EmbedDescriptionText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EmbedFooterText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeniedForReasonEditWindowText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeniedForReasonPermissionsText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EmbedColorSource = table.Column<int>(type: "integer", nullable: false),
                    FallbackEmbedColor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BirthdayConfigs", x => new { x.Name, x.GuildId });
                });

            migrationBuilder.CreateTable(
                name: "Birthdays",
                columns: table => new
                {
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    BirthdayConfigGuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    BirthdayConfigName = table.Column<string>(type: "character varying(32)", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TimeCreatedUtc = table.Column<LocalDateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Birthdays", x => new { x.UserId, x.BirthdayConfigGuildId, x.BirthdayConfigName });
                    table.ForeignKey(
                        name: "FK_Birthdays_BirthdayConfigs_BirthdayConfigName_BirthdayConfig~",
                        columns: x => new { x.BirthdayConfigName, x.BirthdayConfigGuildId },
                        principalTable: "BirthdayConfigs",
                        principalColumns: new[] { "Name", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildConfigs_DefaultBirthdayConfigName_DefaultBirthdayConfi~",
                table: "GuildConfigs",
                columns: new[] { "DefaultBirthdayConfigName", "DefaultBirthdayConfigGuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_Birthdays_BirthdayConfigName_BirthdayConfigGuildId",
                table: "Birthdays",
                columns: new[] { "BirthdayConfigName", "BirthdayConfigGuildId" });

            migrationBuilder.AddForeignKey(
                name: "FK_GuildConfigs_BirthdayConfigs_DefaultBirthdayConfigName_Defa~",
                table: "GuildConfigs",
                columns: new[] { "DefaultBirthdayConfigName", "DefaultBirthdayConfigGuildId" },
                principalTable: "BirthdayConfigs",
                principalColumns: new[] { "Name", "GuildId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuildConfigs_BirthdayConfigs_DefaultBirthdayConfigName_Defa~",
                table: "GuildConfigs");

            migrationBuilder.DropTable(
                name: "Birthdays");

            migrationBuilder.DropTable(
                name: "BirthdayConfigs");

            migrationBuilder.DropIndex(
                name: "IX_GuildConfigs_DefaultBirthdayConfigName_DefaultBirthdayConfi~",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "DefaultBirthdayConfigGuildId",
                table: "GuildConfigs");

            migrationBuilder.DropColumn(
                name: "DefaultBirthdayConfigName",
                table: "GuildConfigs");
        }
    }
}
