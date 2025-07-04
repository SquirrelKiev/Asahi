﻿#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Asahi.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class HighlightsTweaks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequireSendMessagePermissionInChannel",
                table: "HighlightBoards");

            migrationBuilder.AddColumn<int>(
                name: "HighActivityMessageLookBack",
                table: "HighlightThreshold",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HighActivityMessageMaxAgeSeconds",
                table: "HighlightThreshold",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "HighActivityMultiplier",
                table: "HighlightThreshold",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "UniqueUserDecayDelaySeconds",
                table: "HighlightThreshold",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HighActivityMessageLookBack",
                table: "HighlightThreshold");

            migrationBuilder.DropColumn(
                name: "HighActivityMessageMaxAgeSeconds",
                table: "HighlightThreshold");

            migrationBuilder.DropColumn(
                name: "HighActivityMultiplier",
                table: "HighlightThreshold");

            migrationBuilder.DropColumn(
                name: "UniqueUserDecayDelaySeconds",
                table: "HighlightThreshold");

            migrationBuilder.AddColumn<bool>(
                name: "RequireSendMessagePermissionInChannel",
                table: "HighlightBoards",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
