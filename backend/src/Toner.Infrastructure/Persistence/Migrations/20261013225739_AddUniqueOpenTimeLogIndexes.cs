using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueOpenTimeLogIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AUDIT.md H-05. PRECONDICIÓN: ningún técnico ni activo con más de un TimeLog abierto
            // (EndTime IS NULL); si los hay, CREATE UNIQUE INDEX falla y la migración se revierte entera.
            // Verificado con el script de detección de H-05 antes de aplicarla (resultado: 0 y 0).
            migrationBuilder.DropIndex(
                name: "IX_TimeLogs_AssetId",
                table: "TimeLogs");

            migrationBuilder.DropIndex(
                name: "IX_TimeLogs_TechnicianId_Open",
                table: "TimeLogs");

            migrationBuilder.CreateIndex(
                name: "IX_TimeLogs_AssetId_Open",
                table: "TimeLogs",
                column: "AssetId",
                unique: true,
                filter: "\"EndTime\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TimeLogs_TechnicianId_Open",
                table: "TimeLogs",
                column: "TechnicianId",
                unique: true,
                filter: "\"EndTime\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TimeLogs_AssetId_Open",
                table: "TimeLogs");

            migrationBuilder.DropIndex(
                name: "IX_TimeLogs_TechnicianId_Open",
                table: "TimeLogs");

            migrationBuilder.CreateIndex(
                name: "IX_TimeLogs_AssetId",
                table: "TimeLogs",
                column: "AssetId",
                filter: "\"EndTime\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TimeLogs_TechnicianId_Open",
                table: "TimeLogs",
                column: "TechnicianId",
                filter: "\"EndTime\" IS NULL");
        }
    }
}
