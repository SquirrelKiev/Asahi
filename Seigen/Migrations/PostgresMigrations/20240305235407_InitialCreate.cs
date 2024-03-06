using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Seigen.Migrations.PostgresMigrations
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
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedUsersRoles", x => new { x.RoleId, x.UserId });
                });

            migrationBuilder.CreateTable(
                name: "GuildPrefixPreferences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Prefix = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildPrefixPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trackables",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MonitoredGuild = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MonitoredRole = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    AssignableGuild = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    AssignableRole = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    LoggingChannel = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Limit = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trackables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrackedUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TrackableId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
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
