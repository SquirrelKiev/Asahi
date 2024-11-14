using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.SqliteMigrations
{
    /// <inheritdoc />
    public partial class HighlightStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "AssistAuthorId",
                table: "CachedHighlightedMessages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "AuthorId",
                table: "CachedHighlightedMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<DateTime>(
                name: "HighlightedMessageSendDate",
                table: "CachedHighlightedMessages",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "TotalUniqueReactions",
                table: "CachedHighlightedMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "CachedHighlightedMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CachedMessageReactions",
                columns: table => new
                {
                    EmoteName = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    EmoteId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    HighlightedMessageId = table.Column<uint>(type: "INTEGER", nullable: false),
                    IsAnimated = table.Column<bool>(type: "INTEGER", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedMessageReactions", x => new { x.EmoteName, x.EmoteId, x.HighlightedMessageId });
                    table.ForeignKey(
                        name: "FK_CachedMessageReactions_CachedHighlightedMessages_HighlightedMessageId",
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
