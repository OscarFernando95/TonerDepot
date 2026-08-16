using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetInstallationTimeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TimeLogs_ExactlyOneTarget",
                table: "TimeLogs");

            migrationBuilder.AddColumn<Guid>(
                name: "AssetId",
                table: "TimeLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeLogs_AssetId",
                table: "TimeLogs",
                column: "AssetId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TimeLogs_ExactlyOneTarget",
                table: "TimeLogs",
                sql: "(CASE WHEN \"ServiceTicketId\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"MaintenanceOrderId\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"AssetId\" IS NOT NULL THEN 1 ELSE 0 END) = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_TimeLogs_Assets_AssetId",
                table: "TimeLogs",
                column: "AssetId",
                principalTable: "Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TimeLogs_Assets_AssetId",
                table: "TimeLogs");

            migrationBuilder.DropIndex(
                name: "IX_TimeLogs_AssetId",
                table: "TimeLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TimeLogs_ExactlyOneTarget",
                table: "TimeLogs");

            migrationBuilder.DropColumn(
                name: "AssetId",
                table: "TimeLogs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TimeLogs_ExactlyOneTarget",
                table: "TimeLogs",
                sql: "(\"ServiceTicketId\" IS NOT NULL) <> (\"MaintenanceOrderId\" IS NOT NULL)");
        }
    }
}
