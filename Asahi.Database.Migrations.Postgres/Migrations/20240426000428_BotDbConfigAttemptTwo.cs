﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asahi.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class BotDbConfigAttemptTwo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BotWideConfig",
                columns: table => new
                {
                    BotId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ShouldHaveActivity = table.Column<bool>(type: "boolean", nullable: false),
                    UserStatus = table.Column<int>(type: "integer", nullable: false),
                    ActivityType = table.Column<int>(type: "integer", nullable: false),
                    BotActivity = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActivityStreamingUrl = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotWideConfig", x => x.BotId);
                });

            migrationBuilder.CreateTable(
                name: "TrustedIds",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Comment = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PermissionFlags = table.Column<int>(type: "integer", nullable: false),
                    BotWideConfigBotId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustedIds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrustedIds_BotWideConfig_BotWideConfigBotId",
                        column: x => x.BotWideConfigBotId,
                        principalTable: "BotWideConfig",
                        principalColumn: "BotId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrustedIds_BotWideConfigBotId",
                table: "TrustedIds",
                column: "BotWideConfigBotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrustedIds");

            migrationBuilder.DropTable(
                name: "BotWideConfig");
        }
    }
}
