using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.PostgresMigrations
{
    /// <inheritdoc />
    public partial class HighlightsStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AssistAuthorId",
                table: "CachedHighlightedMessages",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AuthorId",
                table: "CachedHighlightedMessages",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "HighlightedMessageSendDate",
                table: "CachedHighlightedMessages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "TotalUniqueReactions",
                table: "CachedHighlightedMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "CachedHighlightedMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CachedMessageReactions",
                columns: table => new
                {
                    EmoteName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EmoteId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    HighlightedMessageId = table.Column<long>(type: "bigint", nullable: false),
                    IsAnimated = table.Column<bool>(type: "boolean", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedMessageReactions", x => new { x.EmoteName, x.EmoteId, x.HighlightedMessageId });
                    table.ForeignKey(
                        name: "FK_CachedMessageReactions_CachedHighlightedMessages_Highlighte~",
                        column: x => x.HighlightedMessageId,
                        principalTable: "CachedHighlightedMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CachedMessageReactions_HighlightedMessageId",
                table: "CachedMessageReactions",
                column: "HighlightedMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedMessageReactions");

            migrationBuilder.DropColumn(
                name: "AssistAuthorId",
                table: "CachedHighlightedMessages");

            migrationBuilder.DropColumn(
                name: "AuthorId",
                table: "CachedHighlightedMessages");

            migrationBuilder.DropColumn(
                name: "HighlightedMessageSendDate",
                table: "CachedHighlightedMessages");

            migrationBuilder.DropColumn(
                name: "TotalUniqueReactions",
                table: "CachedHighlightedMessages");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "CachedHighlightedMessages");
        }
    }
}
