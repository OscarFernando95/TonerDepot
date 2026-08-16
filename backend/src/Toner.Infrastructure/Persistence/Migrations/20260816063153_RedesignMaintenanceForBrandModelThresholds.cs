using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RedesignMaintenanceForBrandModelThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Salvaguarda: si esta migración se aplica sin haber corrido antes la limpieza de datos de
            // prueba, evita que el rename de columnas deje datos con un significado distinto al original
            // (ej. IncludesConsumablesChange -> IncludesUnits no es una migración de dato válida) o que el
            // nuevo AssetModelId NOT NULL falle contra filas de Assets con AssetBrandId pero sin modelo.
            migrationBuilder.Sql("DELETE FROM \"MaintenanceOrders\";");
            migrationBuilder.Sql("DELETE FROM \"MaintenanceSchedules\";");
            migrationBuilder.Sql("DELETE FROM \"ContractAssets\";");
            migrationBuilder.Sql("DELETE FROM \"MeterReadings\";");
            migrationBuilder.Sql("DELETE FROM \"AssetStatusLogs\";");
            migrationBuilder.Sql("UPDATE \"ServiceTickets\" SET \"AssetId\" = NULL;");
            migrationBuilder.Sql("UPDATE \"TimeLogs\" SET \"AssetId\" = NULL;");
            migrationBuilder.Sql("DELETE FROM \"Assets\";");

            migrationBuilder.DropForeignKey(
                name: "FK_Assets_AssetBrands_AssetBrandId",
                table: "Assets");

            migrationBuilder.DropTable(
                name: "AssetTypeMaintenancePolicies");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "Assets");

            migrationBuilder.RenameColumn(
                name: "IncludesConsumablesChange",
                table: "MaintenanceOrders",
                newName: "IncludesUnits");

            migrationBuilder.RenameColumn(
                name: "AssetBrandId",
                table: "Assets",
                newName: "AssetModelId");

            migrationBuilder.RenameIndex(
                name: "IX_Assets_AssetBrandId",
                table: "Assets",
                newName: "IX_Assets_AssetModelId");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUnitsMaintenanceAt",
                table: "MaintenanceSchedules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastUnitsMaintenanceCounter",
                table: "MaintenanceSchedules",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextUnitsDueAt",
                table: "MaintenanceSchedules",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<long>(
                name: "NextUnitsDueCounter",
                table: "MaintenanceSchedules",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesConsumables",
                table: "MaintenanceOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesGeneral",
                table: "MaintenanceOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AssetModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetBrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GeneralPrintThreshold = table.Column<int>(type: "integer", nullable: false),
                    GeneralMonthsInterval = table.Column<int>(type: "integer", nullable: false),
                    UnitsPrintThreshold = table.Column<int>(type: "integer", nullable: false),
                    UnitsMonthsInterval = table.Column<int>(type: "integer", nullable: false),
                    ConsumablesPrintThreshold = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetModels_AssetBrands_AssetBrandId",
                        column: x => x.AssetBrandId,
                        principalTable: "AssetBrands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetModels_AssetBrandId_Name",
                table: "AssetModels",
                columns: new[] { "AssetBrandId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_AssetModels_AssetModelId",
                table: "Assets",
                column: "AssetModelId",
                principalTable: "AssetModels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_AssetModels_AssetModelId",
                table: "Assets");

            migrationBuilder.DropTable(
                name: "AssetModels");

            migrationBuilder.DropColumn(
                name: "LastUnitsMaintenanceAt",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "LastUnitsMaintenanceCounter",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "NextUnitsDueAt",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "NextUnitsDueCounter",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "IncludesConsumables",
                table: "MaintenanceOrders");

            migrationBuilder.DropColumn(
                name: "IncludesGeneral",
                table: "MaintenanceOrders");

            migrationBuilder.RenameColumn(
                name: "IncludesUnits",
                table: "MaintenanceOrders",
                newName: "IncludesConsumablesChange");

            migrationBuilder.RenameColumn(
                name: "AssetModelId",
                table: "Assets",
                newName: "AssetBrandId");

            migrationBuilder.RenameIndex(
                name: "IX_Assets_AssetModelId",
                table: "Assets",
                newName: "IX_Assets_AssetBrandId");

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "Assets",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AssetTypeMaintenancePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ConsumablesPrintThreshold = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeneralMonthsInterval = table.Column<int>(type: "integer", nullable: false),
                    GeneralPrintThreshold = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTypeMaintenancePolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypeMaintenancePolicies_AssetType",
                table: "AssetTypeMaintenancePolicies",
                column: "AssetType",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_AssetBrands_AssetBrandId",
                table: "Assets",
                column: "AssetBrandId",
                principalTable: "AssetBrands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
