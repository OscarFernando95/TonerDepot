using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceOrderDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "MaintenanceOrders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            // Órdenes existentes: sin el dato histórico de qué las disparó, se describe lo que incluyen.
            migrationBuilder.Sql("""
                UPDATE "MaintenanceOrders"
                SET "Description" =
                    CASE WHEN "IsManual" THEN 'Pedida a demanda: ' ELSE 'Mantenimiento: ' END ||
                    concat_ws(', ',
                        CASE WHEN "IncludesGeneral" THEN 'mantenimiento general' END,
                        CASE WHEN "IncludesUnits" THEN 'mantenimiento de unidades' END,
                        CASE WHEN "IncludesConsumables" THEN 'cambio de insumos' END) || '.' ||
                    CASE WHEN "Reason" IS NOT NULL THEN ' Motivo: ' || "Reason" ELSE '' END
                WHERE "Description" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "MaintenanceOrders");
        }
    }
}
