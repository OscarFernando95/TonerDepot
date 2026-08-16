using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RedesignMaintenanceSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El modelo de MaintenanceSchedule cambia de raíz (una regla por fila -> dos sub-reglas por
            // activo); los cronogramas y órdenes manuales de prueba existentes no tienen forma confiable
            // de mapearse al nuevo modelo. Se limpian acá — el backfill (POST /api/maintenance-schedules/backfill)
            // regenera cronogramas correctos para los activos que ya estén instalados bajo un contrato.
            migrationBuilder.Sql("DELETE FROM \"TimeLogs\" WHERE \"MaintenanceOrderId\" IS NOT NULL;");
            migrationBuilder.Sql("DELETE FROM \"AssignmentHistories\" WHERE \"MaintenanceOrderId\" IS NOT NULL;");
            migrationBuilder.Sql("DELETE FROM \"Evidences\" WHERE \"MaintenanceOrderId\" IS NOT NULL;");
            migrationBuilder.Sql("DELETE FROM \"MaintenanceOrders\";");
            migrationBuilder.Sql("DELETE FROM \"MaintenanceSchedules\";");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceSchedules_AssetId",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "FrequencyType",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "PrintThreshold",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "TimeIntervalDays",
                table: "MaintenanceSchedules");

            migrationBuilder.RenameColumn(
                name: "NextDueCounter",
                table: "MaintenanceSchedules",
                newName: "LastGeneralMaintenanceCounter");

            migrationBuilder.RenameColumn(
                name: "NextDueAt",
                table: "MaintenanceSchedules",
                newName: "LastGeneralMaintenanceAt");

            migrationBuilder.RenameColumn(
                name: "LastExecutedCounter",
                table: "MaintenanceSchedules",
                newName: "LastConsumablesChangeCounter");

            migrationBuilder.RenameColumn(
                name: "LastExecutedAt",
                table: "MaintenanceSchedules",
                newName: "LastConsumablesChangeAt");

            migrationBuilder.AlterColumn<Guid>(
                name: "ContractId",
                table: "MaintenanceSchedules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NextConsumablesDueCounter",
                table: "MaintenanceSchedules",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextGeneralDueAt",
                table: "MaintenanceSchedules",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<long>(
                name: "NextGeneralDueCounter",
                table: "MaintenanceSchedules",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesConsumablesChange",
                table: "MaintenanceOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AssetTypeMaintenancePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    GeneralPrintThreshold = table.Column<int>(type: "integer", nullable: false),
                    GeneralMonthsInterval = table.Column<int>(type: "integer", nullable: false),
                    ConsumablesPrintThreshold = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTypeMaintenancePolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_AssetId",
                table: "MaintenanceSchedules",
                column: "AssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypeMaintenancePolicies_AssetType",
                table: "AssetTypeMaintenancePolicies",
                column: "AssetType",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetTypeMaintenancePolicies");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceSchedules_AssetId",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "NextConsumablesDueCounter",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "NextGeneralDueAt",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "NextGeneralDueCounter",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "IncludesConsumablesChange",
                table: "MaintenanceOrders");

            migrationBuilder.RenameColumn(
                name: "LastGeneralMaintenanceCounter",
                table: "MaintenanceSchedules",
                newName: "NextDueCounter");

            migrationBuilder.RenameColumn(
                name: "LastGeneralMaintenanceAt",
                table: "MaintenanceSchedules",
                newName: "NextDueAt");

            migrationBuilder.RenameColumn(
                name: "LastConsumablesChangeCounter",
                table: "MaintenanceSchedules",
                newName: "LastExecutedCounter");

            migrationBuilder.RenameColumn(
                name: "LastConsumablesChangeAt",
                table: "MaintenanceSchedules",
                newName: "LastExecutedAt");

            migrationBuilder.AlterColumn<Guid>(
                name: "ContractId",
                table: "MaintenanceSchedules",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "FrequencyType",
                table: "MaintenanceSchedules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PrintThreshold",
                table: "MaintenanceSchedules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeIntervalDays",
                table: "MaintenanceSchedules",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_AssetId",
                table: "MaintenanceSchedules",
                column: "AssetId");
        }
    }
}
