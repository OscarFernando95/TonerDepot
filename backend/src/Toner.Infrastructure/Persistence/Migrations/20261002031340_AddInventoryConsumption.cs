using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryConsumption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssetId",
                table: "InventoryMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "InventoryMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CounterValue",
                table: "InventoryMovements",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MaintenanceOrderId",
                table: "InventoryMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceTicketId",
                table: "InventoryMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TimeLogId",
                table: "InventoryMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_AssetId_OccurredAt",
                table: "InventoryMovements",
                columns: new[] { "AssetId", "OccurredAt" },
                filter: "\"AssetId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ClientId",
                table: "InventoryMovements",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_MaintenanceOrderId",
                table: "InventoryMovements",
                column: "MaintenanceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ServiceTicketId",
                table: "InventoryMovements",
                column: "ServiceTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_TimeLogId",
                table: "InventoryMovements",
                column: "TimeLogId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_Assets_AssetId",
                table: "InventoryMovements",
                column: "AssetId",
                principalTable: "Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_MaintenanceOrders_MaintenanceOrderId",
                table: "InventoryMovements",
                column: "MaintenanceOrderId",
                principalTable: "MaintenanceOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_ServiceTickets_ServiceTicketId",
                table: "InventoryMovements",
                column: "ServiceTicketId",
                principalTable: "ServiceTickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_TimeLogs_TimeLogId",
                table: "InventoryMovements",
                column: "TimeLogId",
                principalTable: "TimeLogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ── RLS de InventoryMovements ────────────────────────────────────────────────────────
            // El consumo cuelga de un cliente (ClientId, capturado al escribir desde la orden/ticket/activo), así que
            // la tabla pasa a necesitar política (CLAUDE.md: RLS en toda tabla con datos multi-cliente). Mismo patrón
            // que la fase 3b: comparación directa con ::uuid y políticas separadas por rol. Los movimientos de la
            // empresa (entradas, traspasos, ajustes) tienen ClientId NULL: ningún cliente los ve (fail-closed) y el
            // staff — que incluye al técnico en campo — los ve todos.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'toner_app_staff') THEN
                    RAISE EXCEPTION
                      'Falta el rol toner_app_staff. Corre docker/postgres/create-app-role.sh contra '
                      'esta base antes de aplicar la migración.';
                  END IF;
                END
                $$;

                ALTER TABLE ""InventoryMovements"" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ""InventoryMovements"" FORCE ROW LEVEL SECURITY;

                CREATE POLICY inventorymovements_client_isolation ON ""InventoryMovements""
                  FOR ALL TO toner_app
                  USING      (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid)
                  WITH CHECK (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid);

                CREATE POLICY inventorymovements_staff_access ON ""InventoryMovements""
                  FOR ALL TO toner_app_staff
                  USING (true) WITH CHECK (true);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS inventorymovements_client_isolation ON ""InventoryMovements"";
                DROP POLICY IF EXISTS inventorymovements_staff_access ON ""InventoryMovements"";
                ALTER TABLE ""InventoryMovements"" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE ""InventoryMovements"" DISABLE ROW LEVEL SECURITY;
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_Assets_AssetId",
                table: "InventoryMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_MaintenanceOrders_MaintenanceOrderId",
                table: "InventoryMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_ServiceTickets_ServiceTicketId",
                table: "InventoryMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_TimeLogs_TimeLogId",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_AssetId_OccurredAt",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_ClientId",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_MaintenanceOrderId",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_ServiceTicketId",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_TimeLogId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "AssetId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "CounterValue",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "MaintenanceOrderId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "ServiceTicketId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "TimeLogId",
                table: "InventoryMovements");
        }
    }
}
