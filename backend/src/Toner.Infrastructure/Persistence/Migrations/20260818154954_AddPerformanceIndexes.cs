using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TimeLogs_AssetId",
                table: "TimeLogs");

            migrationBuilder.DropIndex(
                name: "IX_MeterReadings_AssetId",
                table: "MeterReadings");

            migrationBuilder.CreateIndex(
                name: "IX_TimeLogs_AssetId",
                table: "TimeLogs",
                column: "AssetId",
                filter: "\"EndTime\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TimeLogs_StartTime",
                table: "TimeLogs",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_TimeLogs_TechnicianId_Open",
                table: "TimeLogs",
                column: "TechnicianId",
                filter: "\"EndTime\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_CreatedAt",
                table: "ServiceTickets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_ResolvedAt",
                table: "ServiceTickets",
                column: "ResolvedAt",
                filter: "\"ResolvedAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_Status",
                table: "ServiceTickets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_AssetId_ReadingDate",
                table: "MeterReadings",
                columns: new[] { "AssetId", "ReadingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrders_CompletedAt",
                table: "MaintenanceOrders",
                column: "CompletedAt",
                filter: "\"CompletedAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrders_CreatedAt",
                table: "MaintenanceOrders",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_LifecycleStatus",
                table: "Assets",
                column: "LifecycleStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TimeLogs_AssetId",
                table: "TimeLogs");

            migrationBuilder.DropIndex(
                name: "IX_TimeLogs_StartTime",
                table: "TimeLogs");

            migrationBuilder.DropIndex(
                name: "IX_TimeLogs_TechnicianId_Open",
                table: "TimeLogs");

            migrationBuilder.DropIndex(
                name: "IX_ServiceTickets_CreatedAt",
                table: "ServiceTickets");

            migrationBuilder.DropIndex(
                name: "IX_ServiceTickets_ResolvedAt",
                table: "ServiceTickets");

            migrationBuilder.DropIndex(
                name: "IX_ServiceTickets_Status",
                table: "ServiceTickets");

            migrationBuilder.DropIndex(
                name: "IX_MeterReadings_AssetId_ReadingDate",
                table: "MeterReadings");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceOrders_CompletedAt",
                table: "MaintenanceOrders");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceOrders_CreatedAt",
                table: "MaintenanceOrders");

            migrationBuilder.DropIndex(
                name: "IX_Assets_LifecycleStatus",
                table: "Assets");

            migrationBuilder.CreateIndex(
                name: "IX_TimeLogs_AssetId",
                table: "TimeLogs",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_AssetId",
                table: "MeterReadings",
                column: "AssetId");
        }
    }
}
