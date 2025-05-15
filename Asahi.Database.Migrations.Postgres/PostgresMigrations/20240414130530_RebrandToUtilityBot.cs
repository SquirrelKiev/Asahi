using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Asahi.Migrations.PostgresMigrations
{
    /// <inheritdoc />
    public partial class RebrandToUtilityBot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildPrefixPreferences");

            migrationBuilder.CreateTable(
                name: "CustomCommands",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    OwnerId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsRaw = table.Column<bool>(type: "boolean", nullable: false),
                    Contents = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuildConfigs",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Prefix = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildConfigs", x => x.GuildId);
                });

            migrationBuilder.CreateTable(
                name: "HighlightBoards",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    LoggingChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Threshold = table.Column<long>(type: "bigint", nullable: false),
                    FilteredChannelsIsBlockList = table.Column<bool>(type: "boolean", nullable: false),
                    FilteredChannels = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false),
                    MaxMessageAgeSeconds = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HighlightBoards", x => new { x.GuildId, x.Name });
                });

            migrationBuilder.CreateTable(
                name: "CachedHighlightedMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OriginalMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    HighlightMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    HighlightBoardGuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    HighlightBoardName = table.Column<string>(type: "character varying(32)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedHighlightedMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CachedHighlightedMessages_HighlightBoards_HighlightBoardGui~",
                        columns: x => new { x.HighlightBoardGuildId, x.HighlightBoardName },
                        principalTable: "HighlightBoards",
                        principalColumns: new[] { "GuildId", "Name" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CachedHighlightedMessages_HighlightBoardGuildId_HighlightBo~",
                table: "CachedHighlightedMessages",
                columns: new[] { "HighlightBoardGuildId", "HighlightBoardName" });

            migrationBuilder.CreateIndex(
                name: "IX_CachedHighlightedMessages_HighlightMessageId",
                table: "CachedHighlightedMessages",
                column: "HighlightMessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedHighlightedMessages");

            migrationBuilder.DropTable(
                name: "CustomCommands");

            migrationBuilder.DropTable(
                name: "GuildConfigs");

            migrationBuilder.DropTable(
                name: "HighlightBoards");

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
        }
    }
}
