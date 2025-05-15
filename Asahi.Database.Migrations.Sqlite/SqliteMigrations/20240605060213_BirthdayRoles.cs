#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Asahi.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class BirthdayRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "DefaultBirthdayConfigGuildId",
                table: "GuildConfigs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultBirthdayConfigName",
                table: "GuildConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BirthdayConfigs",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    BirthdayRole = table.Column<ulong>(type: "INTEGER", nullable: false),
                    AllowedRoles = table.Column<string>(type: "TEXT", nullable: false),
                    DeniedRoles = table.Column<string>(type: "TEXT", nullable: false),
                    EditWindowSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    EmbedTitleText = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EmbedDescriptionText = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EmbedFooterText = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DeniedForReasonEditWindowText = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DeniedForReasonPermissionsText = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EmbedColorSource = table.Column<int>(type: "INTEGER", nullable: false),
                    FallbackEmbedColor = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BirthdayConfigs", x => new { x.Name, x.GuildId });
                });

            migrationBuilder.CreateTable(
                name: "Birthdays",
                columns: table => new
                {
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    BirthdayConfigGuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    BirthdayConfigName = table.Column<string>(type: "TEXT", nullable: false),
                    Day = table.Column<int>(type: "INTEGER", nullable: false),
                    Month = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeZone = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TimeCreatedUtc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Birthdays", x => new { x.UserId, x.BirthdayConfigGuildId, x.BirthdayConfigName });
                    table.ForeignKey(
                        name: "FK_Birthdays_BirthdayConfigs_BirthdayConfigName_BirthdayConfigGuildId",
                        columns: x => new { x.BirthdayConfigName, x.BirthdayConfigGuildId },
                        principalTable: "BirthdayConfigs",
                        principalColumns: new[] { "Name", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildConfigs_DefaultBirthdayConfigName_DefaultBirthdayConfigGuildId",
                table: "GuildConfigs",
                columns: new[] { "DefaultBirthdayConfigName", "DefaultBirthdayConfigGuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_Birthdays_BirthdayConfigName_BirthdayConfigGuildId",
                table: "Birthdays",
                columns: new[] { "BirthdayConfigName", "BirthdayConfigGuildId" });

            migrationBuilder.AddForeignKey(
                name: "FK_GuildConfigs_BirthdayConfigs_DefaultBirthdayConfigName_DefaultBirthdayConfigGuildId",
                table: "GuildConfigs",
                columns: new[] { "DefaultBirthdayConfigName", "DefaultBirthdayConfigGuildId" },
                principalTable: "BirthdayConfigs",
                principalColumns: new[] { "Name", "GuildId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuildConfigs_BirthdayConfigs_DefaultBirthdayConfigName_DefaultBirthdayConfigGuildId",
                table: "GuildConfigs");

            migrationBuilder.DropTable(
                name: "Birthdays");

            migrationBuilder.DropTable(
                name: "BirthdayConfigs");

            migrationBuilder.DropIndex(
                name: "IX_GuildConfigs_DefaultBirthdayConfigName_DefaultBirthdayConfigGuildId",
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
