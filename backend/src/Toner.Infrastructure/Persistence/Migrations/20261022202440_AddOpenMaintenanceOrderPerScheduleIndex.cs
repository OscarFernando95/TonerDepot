using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenMaintenanceOrderPerScheduleIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrders_MaintenanceScheduleId_Open",
                table: "MaintenanceOrders",
                column: "MaintenanceScheduleId",
                unique: true,
                filter: "\"Status\" IN ('Pendiente', 'Asignada', 'EnProceso')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MaintenanceOrders_MaintenanceScheduleId_Open",
                table: "MaintenanceOrders");
        }
    }
}
