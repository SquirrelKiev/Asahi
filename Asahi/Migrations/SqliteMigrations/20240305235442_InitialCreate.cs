using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.SqliteMigrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CachedUsersRoles",
                columns: table => new
                {
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    RoleId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedUsersRoles", x => new { x.RoleId, x.UserId });
                });

            migrationBuilder.CreateTable(
                name: "GuildPrefixPreferences",
                columns: table => new
                {
                    Id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildPrefixPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trackables",
                columns: table => new
                {
                    Id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MonitoredGuild = table.Column<ulong>(type: "INTEGER", nullable: false),
                    MonitoredRole = table.Column<ulong>(type: "INTEGER", nullable: false),
                    AssignableGuild = table.Column<ulong>(type: "INTEGER", nullable: false),
                    AssignableRole = table.Column<ulong>(type: "INTEGER", nullable: false),
                    LoggingChannel = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Limit = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trackables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrackedUsers",
                columns: table => new
                {
                    Id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TrackableId = table.Column<uint>(type: "INTEGER", nullable: false),
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackedUsers_Trackables_TrackableId",
                        column: x => x.TrackableId,
                        principalTable: "Trackables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CachedUsersRoles_RoleId",
                table: "CachedUsersRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Trackables_AssignableRole_MonitoredRole",
                table: "Trackables",
                columns: new[] { "AssignableRole", "MonitoredRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackedUsers_TrackableId_UserId",
                table: "TrackedUsers",
                columns: new[] { "TrackableId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedUsersRoles");

            migrationBuilder.DropTable(
                name: "GuildPrefixPreferences");

            migrationBuilder.DropTable(
                name: "TrackedUsers");

            migrationBuilder.DropTable(
                name: "Trackables");
        }
    }
}
