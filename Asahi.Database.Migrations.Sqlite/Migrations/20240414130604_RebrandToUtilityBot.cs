#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Asahi.Migrations.Sqlite.Migrations
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
                    Id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsRaw = table.Column<bool>(type: "INTEGER", nullable: false),
                    Contents = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuildConfigs",
                columns: table => new
                {
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildConfigs", x => x.GuildId);
                });

            migrationBuilder.CreateTable(
                name: "HighlightBoards",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    LoggingChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Threshold = table.Column<uint>(type: "INTEGER", nullable: false),
                    FilteredChannelsIsBlockList = table.Column<bool>(type: "INTEGER", nullable: false),
                    FilteredChannels = table.Column<string>(type: "TEXT", nullable: false),
                    MaxMessageAgeSeconds = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HighlightBoards", x => new { x.GuildId, x.Name });
                });

            migrationBuilder.CreateTable(
                name: "CachedHighlightedMessages",
                columns: table => new
                {
                    Id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OriginalMessageId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    HighlightMessageId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    HighlightBoardGuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    HighlightBoardName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedHighlightedMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CachedHighlightedMessages_HighlightBoards_HighlightBoardGuildId_HighlightBoardName",
                        columns: x => new { x.HighlightBoardGuildId, x.HighlightBoardName },
                        principalTable: "HighlightBoards",
                        principalColumns: new[] { "GuildId", "Name" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CachedHighlightedMessages_HighlightBoardGuildId_HighlightBoardName",
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
                    Id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildPrefixPreferences", x => x.Id);
                });
        }
    }
}
