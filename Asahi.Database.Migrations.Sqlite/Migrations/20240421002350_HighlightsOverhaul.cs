#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Asahi.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class HighlightsOverhaul : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedHighlightedMessages");

            migrationBuilder.DropTable(
                name: "HighlightBoards");

            migrationBuilder.CreateTable(
                name: "HighlightBoards",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    LoggingChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    RequireSendMessagePermissionInChannel = table.Column<bool>(type: "INTEGER", nullable: false),
                    FilterSelfReactions = table.Column<bool>(type: "INTEGER", nullable: false),
                    FilteredChannelsIsBlockList = table.Column<bool>(type: "INTEGER", nullable: false),
                    FilteredChannels = table.Column<string>(type: "TEXT", nullable: false),
                    MaxMessageAgeSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    EmbedColorSource = table.Column<int>(type: "INTEGER", nullable: false),
                    FallbackEmbedColor = table.Column<uint>(type: "INTEGER", nullable: false),
                    AutoReactMaxAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoReactMaxReactions = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoReactEmoteChoicePreference = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoReactFallbackEmoji = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
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
                    HighlightMessageIds = table.Column<string>(type: "TEXT", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "EmoteAlias",
                columns: table => new
                {
                    EmoteName = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    HighlightBoardGuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    HighlightBoardName = table.Column<string>(type: "TEXT", nullable: false),
                    EmoteReplacement = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmoteAlias", x => new { x.EmoteName, x.HighlightBoardGuildId, x.HighlightBoardName });
                    table.ForeignKey(
                        name: "FK_EmoteAlias_HighlightBoards_HighlightBoardGuildId_HighlightBoardName",
                        columns: x => new { x.HighlightBoardGuildId, x.HighlightBoardName },
                        principalTable: "HighlightBoards",
                        principalColumns: new[] { "GuildId", "Name" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HighlightThreshold",
                columns: table => new
                {
                    OverrideId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    HighlightBoardGuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    HighlightBoardName = table.Column<string>(type: "TEXT", nullable: false),
                    BaseThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    UniqueUserMessageMaxAgeSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    UniqueUserMultiplier = table.Column<float>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HighlightThreshold", x => new { x.OverrideId, x.HighlightBoardGuildId, x.HighlightBoardName });
                    table.ForeignKey(
                        name: "FK_HighlightThreshold_HighlightBoards_HighlightBoardGuildId_HighlightBoardName",
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
                name: "IX_CachedHighlightedMessages_HighlightMessageIds",
                table: "CachedHighlightedMessages",
                column: "HighlightMessageIds",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmoteAlias_HighlightBoardGuildId_HighlightBoardName",
                table: "EmoteAlias",
                columns: new[] { "HighlightBoardGuildId", "HighlightBoardName" });

            migrationBuilder.CreateIndex(
                name: "IX_HighlightThreshold_HighlightBoardGuildId_HighlightBoardName",
                table: "HighlightThreshold",
                columns: new[] { "HighlightBoardGuildId", "HighlightBoardName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedHighlightedMessages");

            migrationBuilder.DropTable(
                name: "EmoteAlias");

            migrationBuilder.DropTable(
                name: "HighlightThreshold");

            migrationBuilder.DropTable(
                name: "HighlightBoards");

            migrationBuilder.CreateTable(
                name: "HighlightBoards",
                columns: table => new
                {
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    FilteredChannels = table.Column<string>(type: "TEXT", nullable: false),
                    FilteredChannelsIsBlockList = table.Column<bool>(type: "INTEGER", nullable: false),
                    LoggingChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    MaxMessageAgeSeconds = table.Column<uint>(type: "INTEGER", nullable: false),
                    Threshold = table.Column<uint>(type: "INTEGER", nullable: false)
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
                    HighlightBoardGuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    HighlightBoardName = table.Column<string>(type: "TEXT", nullable: false),
                    HighlightMessageId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    OriginalMessageId = table.Column<ulong>(type: "INTEGER", nullable: false)
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
    }
}
