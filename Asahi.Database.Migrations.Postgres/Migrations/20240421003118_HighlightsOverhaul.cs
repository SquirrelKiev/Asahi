using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Asahi.Migrations.Postgres.Migrations
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
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    LoggingChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RequireSendMessagePermissionInChannel = table.Column<bool>(type: "boolean", nullable: false),
                    FilterSelfReactions = table.Column<bool>(type: "boolean", nullable: false),
                    FilteredChannelsIsBlockList = table.Column<bool>(type: "boolean", nullable: false),
                    FilteredChannels = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false),
                    MaxMessageAgeSeconds = table.Column<int>(type: "integer", nullable: false),
                    EmbedColorSource = table.Column<int>(type: "integer", nullable: false),
                    FallbackEmbedColor = table.Column<long>(type: "bigint", nullable: false),
                    AutoReactMaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    AutoReactMaxReactions = table.Column<int>(type: "integer", nullable: false),
                    AutoReactEmoteChoicePreference = table.Column<int>(type: "integer", nullable: false),
                    AutoReactFallbackEmoji = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
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
                    HighlightMessageIds = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "EmoteAlias",
                columns: table => new
                {
                    EmoteName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HighlightBoardGuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    HighlightBoardName = table.Column<string>(type: "character varying(32)", nullable: false),
                    EmoteReplacement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmoteAlias", x => new { x.EmoteName, x.HighlightBoardGuildId, x.HighlightBoardName });
                    table.ForeignKey(
                        name: "FK_EmoteAlias_HighlightBoards_HighlightBoardGuildId_HighlightB~",
                        columns: x => new { x.HighlightBoardGuildId, x.HighlightBoardName },
                        principalTable: "HighlightBoards",
                        principalColumns: new[] { "GuildId", "Name" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HighlightThreshold",
                columns: table => new
                {
                    OverrideId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    HighlightBoardGuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    HighlightBoardName = table.Column<string>(type: "character varying(32)", nullable: false),
                    BaseThreshold = table.Column<int>(type: "integer", nullable: false),
                    MaxThreshold = table.Column<int>(type: "integer", nullable: false),
                    UniqueUserMessageMaxAgeSeconds = table.Column<int>(type: "integer", nullable: false),
                    UniqueUserMultiplier = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HighlightThreshold", x => new { x.OverrideId, x.HighlightBoardGuildId, x.HighlightBoardName });
                    table.ForeignKey(
                        name: "FK_HighlightThreshold_HighlightBoards_HighlightBoardGuildId_Hi~",
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
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FilteredChannels = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: false),
                    FilteredChannelsIsBlockList = table.Column<bool>(type: "boolean", nullable: false),
                    LoggingChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MaxMessageAgeSeconds = table.Column<long>(type: "bigint", nullable: false),
                    Threshold = table.Column<long>(type: "bigint", nullable: false)
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
                    HighlightBoardGuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    HighlightBoardName = table.Column<string>(type: "character varying(32)", nullable: false),
                    HighlightMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    OriginalMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
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
    }
}
