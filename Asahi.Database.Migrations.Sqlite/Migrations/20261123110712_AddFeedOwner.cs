using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.Sqlite.Migrations;

/// <inheritdoc />
public partial class _20261123110712_AddFeedOwner : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<ulong>(
            name: "OwnerUserId",
            table: "RssFeedListeners",
            type: "INTEGER",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "OwnerUserId",
            table: "RssFeedListeners");
    }
}
