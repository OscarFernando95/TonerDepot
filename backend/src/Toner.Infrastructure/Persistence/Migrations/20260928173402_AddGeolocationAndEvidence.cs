using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGeolocationAndEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CheckInAccuracyMeters",
                table: "TimeLogs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInDistanceMeters",
                table: "TimeLogs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInLatitude",
                table: "TimeLogs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckInLocationStatus",
                table: "TimeLogs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInLongitude",
                table: "TimeLogs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutAccuracyMeters",
                table: "TimeLogs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutDistanceMeters",
                table: "TimeLogs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutLatitude",
                table: "TimeLogs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckOutLocationStatus",
                table: "TimeLogs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutLongitude",
                table: "TimeLogs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "Evidences",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Evidences",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "Evidences",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "TimeLogId",
                table: "Evidences",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "ClientLocations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "ClientLocations",
                type: "double precision",
                nullable: true);

            // Backfill desde el padre (ticket u orden) y luego NOT NULL, igual que las tablas de la fase 3b.
            // Antes de este cambio Evidences no tenía ningún camino de escritura, así que en la práctica
            // está vacía; se escribe igual para que la migración sea correcta contra cualquier base.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'toner_app_staff') THEN
                    RAISE EXCEPTION 'Falta el rol toner_app_staff. Corre docker/postgres/create-app-role.sh antes.';
                  END IF;
                END
                $$;

                UPDATE ""Evidences"" e SET ""ClientId"" = t.""ClientId""
                FROM ""ServiceTickets"" t WHERE t.""Id"" = e.""ServiceTicketId"" AND e.""ClientId"" IS NULL;

                UPDATE ""Evidences"" e SET ""ClientId"" = o.""ClientId""
                FROM ""MaintenanceOrders"" o WHERE o.""Id"" = e.""MaintenanceOrderId"" AND e.""ClientId"" IS NULL;
            ");

            migrationBuilder.AlterColumn<Guid>(name: "ClientId", table: "Evidences", type: "uuid", nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_Evidences_ClientId",
                table: "Evidences",
                column: "ClientId");

            // Políticas RLS partidas por rol (misma plantilla que 3b). Una foto de un ticket pertenece a
            // un cliente: el rol Cliente solo vería las suyas, y aunque hoy ningún endpoint las expone a
            // clientes, la política deja la red debajo para el día que se abra.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Evidences"" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ""Evidences"" FORCE ROW LEVEL SECURITY;

                CREATE POLICY evidences_client_isolation ON ""Evidences""
                  FOR ALL TO toner_app
                  USING      (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid)
                  WITH CHECK (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid);

                CREATE POLICY evidences_staff_access ON ""Evidences""
                  FOR ALL TO toner_app_staff
                  USING (true) WITH CHECK (true);
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Evidences_TimeLogId",
                table: "Evidences",
                column: "TimeLogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS evidences_client_isolation ON ""Evidences"";
                DROP POLICY IF EXISTS evidences_staff_access ON ""Evidences"";
                ALTER TABLE ""Evidences"" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE ""Evidences"" DISABLE ROW LEVEL SECURITY;
            ");

            migrationBuilder.DropIndex(
                name: "IX_Evidences_ClientId",
                table: "Evidences");

            migrationBuilder.DropIndex(
                name: "IX_Evidences_TimeLogId",
                table: "Evidences");

            migrationBuilder.DropColumn(
                name: "CheckInAccuracyMeters",
                table: "TimeLogs");

            migrationBuilder.DropColumn(
                name: "CheckInDistanceMeters",
                table: "TimeLogs");

            migrationBuilder.DropColumn(
                name: "CheckInLatitude",
                table: "TimeLogs");

            migrationBuilder.DropColumn(
                name: "CheckInLocationStatus",
                table: "TimeLogs");

            migrationBuilder.DropColumn(
                name: "CheckInLongitude",
                table: "TimeLogs");

            migrationBuilder.DropColumn(
                name: "CheckOutAccuracyMeters",
                table: "TimeLogs");

            migrationBuilder.DropColumn(
                name: "CheckOutDistanceMeters",
                table: "TimeLogs");

            migrationBuilder.DropColumn(
                name: "CheckOutLatitude",
                table: "TimeLogs");

            migrationBuilder.DropColumn(
                name: "CheckOutLocationStatus",
                table: "TimeLogs");

            migrationBuilder.DropColumn(
                name: "CheckOutLongitude",
                table: "TimeLogs");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Evidences");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Evidences");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "Evidences");

            migrationBuilder.DropColumn(
                name: "TimeLogId",
                table: "Evidences");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "ClientLocations");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "ClientLocations");
        }
    }
}
