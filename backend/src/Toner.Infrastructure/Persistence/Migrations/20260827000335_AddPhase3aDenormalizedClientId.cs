using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Fase 3a de RLS: denormaliza ClientId en ServiceTickets y Assets, y reescribe sus políticas de
    /// EXISTS a comparación directa (SECURITY_AUDIT_V2.md).
    ///
    /// Por qué: son las dos únicas tablas cuyo predicado de Cliente sigue siendo un EXISTS contra
    /// ClientLocations. SplitRlsPoliciesByRole arregló el Seq Scan de ClientLocations y Contracts
    /// (comparación directa → Index Scan), pero un EXISTS no es sargable: Postgres lo resuelve como
    /// `hashed SubPlan` dentro de un Filter y escanea la tabla entera. Medido con EXPLAIN ANALYZE sobre
    /// 8000 activos: Seq Scan cost=0.00..8607.18. Con la columna denormalizada + índice, el mismo
    /// volumen da Bitmap Index Scan cost=51.29..224.29 y el predicado pasa de Filter a Index Cond.
    ///
    /// Las dos columnas se pueblan de forma DISTINTA a propósito:
    ///
    /// - ServiceTickets.ClientId → CAPTURA AL ESCRIBIR (lo setea ServiceTicketService.CreateAsync).
    ///   ClientLocation.ClientId es inmutable, así que capturar y derivar dan el mismo valor por
    ///   siempre. NOT NULL: hace imposible insertar un ticket sin decidir el cliente.
    ///
    /// - Assets.ClientId → DERIVADO Y SINCRONIZADO por trigger. Es un estado ACTUAL, no un hecho
    ///   histórico: CurrentClientLocationId es mutable (un equipo vuelve a bodega y se reinstala en
    ///   otro cliente), así que la columna debe seguir al padre. Nullable: en bodega no hay cliente.
    /// </summary>
    public partial class AddPhase3aDenormalizedClientId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Guarda ───────────────────────────────────────────────────────────────────────────
            // Esta migración hace DML (los UPDATE del backfill) sobre dos tablas con FORCE ROW LEVEL
            // SECURITY y políticas restringidas por rol. Un rol de migraciones que no herede de
            // toner_app_staff no coincide con ninguna política: el backfill afectaría 0 filas EN
            // SILENCIO y el NOT NULL de más abajo fallaría con un error críptico. No se reproduce en
            // desarrollo (ahí el owner es superusuario y bypasea RLS), de ahí la comprobación explícita.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                  IF NOT (SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user)
                     AND NOT pg_has_role(current_user, 'toner_app_staff', 'usage') THEN
                    RAISE EXCEPTION
                      'El rol de migraciones (%) no es superusuario ni hereda de toner_app_staff. El '
                      'backfill de esta migración afectaría 0 filas EN SILENCIO. Corre '
                      'docker/postgres/create-app-role.sh primero.', current_user;
                  END IF;
                END
                $$;
            ");

            // ── Columnas (ambas nullable al principio: el backfill va después) ───────────────────
            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "ServiceTickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "Assets",
                type: "uuid",
                nullable: true);

            // ── Backfill ─────────────────────────────────────────────────────────────────────────
            // Ambos derivan del padre vigente. Para ServiceTickets eso es exacto y definitivo:
            // ClientLocation.ClientId es inmutable, así que el valor de hoy es el mismo que tenía el
            // ticket el día que se creó — no hay estado histórico que reconstruir. Para Assets es la
            // definición misma de la columna (estado actual).
            migrationBuilder.Sql(@"
                UPDATE ""ServiceTickets"" t
                SET ""ClientId"" = cl.""ClientId""
                FROM ""ClientLocations"" cl
                WHERE cl.""Id"" = t.""ClientLocationId"" AND t.""ClientId"" IS NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE ""Assets"" a
                SET ""ClientId"" = cl.""ClientId""
                FROM ""ClientLocations"" cl
                WHERE cl.""Id"" = a.""CurrentClientLocationId"" AND a.""ClientId"" IS NULL;
            ");

            // Aborta con un mensaje entendible en vez de dejar que falle el NOT NULL de abajo con un
            // error de constraint. Solo aplica a ServiceTickets: en Assets, NULL es un valor válido
            // (activo en bodega).
            migrationBuilder.Sql(@"
                DO $$
                DECLARE huerfanos int;
                BEGIN
                  SELECT count(*) INTO huerfanos FROM ""ServiceTickets"" WHERE ""ClientId"" IS NULL;
                  IF huerfanos > 0 THEN
                    RAISE EXCEPTION
                      'Backfill incompleto: % tickets sin ClientId derivable desde su ClientLocation. '
                      'Revisa si hay ClientLocationId huérfanos antes de reintentar.', huerfanos;
                  END IF;
                END
                $$;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                table: "ServiceTickets",
                type: "uuid",
                nullable: false);

            // ── Índices: sin ellos la política no resuelve por Index Scan y todo esto no sirve ──
            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_ClientId",
                table: "ServiceTickets",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_ClientId",
                table: "Assets",
                column: "ClientId");

            // ── Trigger de sincronización de Assets ──────────────────────────────────────────────
            // ⚠️ EXCEPCIÓN DELIBERADA a la convención de CLAUDE.md de no tener lógica fuera de la capa
            // de aplicación. El argumento: esto NO es una regla de dominio, es INTEGRIDAD REFERENCIAL
            // DERIVADA — está más cerca de una FOREIGN KEY o una columna calculada que de una decisión
            // de negocio. No decide nada; solo garantiza que una copia denormalizada no se separe de
            // su origen.
            //
            // Por qué no basta con disciplina en C#: la sincronización tiene que ocurrir en CADA
            // UPDATE de CurrentClientLocationId, y hoy hay varios caminos que mueven un activo
            // (instalación, devolución a bodega, baja). Que la seguridad dependa de que los cuatro —y
            // el quinto que se escriba mañana— se acuerden, es exactamente lo que un trigger elimina.
            //
            // Se dispara en TODO INSERT/UPDATE, no solo `UPDATE OF "CurrentClientLocationId"`: así un
            // UPDATE que tocara "ClientId" directamente (a mano, o por un bug) queda recalculado desde
            // el origen en vez de persistirse. El costo es una búsqueda por PK por fila actualizada.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION sync_asset_client_id() RETURNS trigger AS $$
                BEGIN
                  NEW.""ClientId"" := (
                    SELECT cl.""ClientId"" FROM ""ClientLocations"" cl
                    WHERE cl.""Id"" = NEW.""CurrentClientLocationId""
                  );
                  RETURN NEW;
                END
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS assets_sync_client_id ON ""Assets"";
                CREATE TRIGGER assets_sync_client_id
                  BEFORE INSERT OR UPDATE ON ""Assets""
                  FOR EACH ROW EXECUTE FUNCTION sync_asset_client_id();
            ");

            // ── Políticas: de EXISTS a comparación directa ───────────────────────────────────────
            // Mismo patrón partido por rol de SplitRlsPoliciesByRole. La de staff no cambia de forma
            // (USING (true)) pero se recrea junto con la otra para que ambas queden definidas en un
            // solo lugar y no haya que buscarlas en dos migraciones distintas.
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS service_tickets_client_isolation ON ""ServiceTickets"";
                DROP POLICY IF EXISTS service_tickets_staff_access ON ""ServiceTickets"";

                CREATE POLICY service_tickets_client_isolation ON ""ServiceTickets""
                  FOR ALL TO toner_app
                  USING      (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid)
                  WITH CHECK (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid);

                CREATE POLICY service_tickets_staff_access ON ""ServiceTickets""
                  FOR ALL TO toner_app_staff
                  USING (true) WITH CHECK (true);
            ");

            // En Assets, ClientId NULL (bodega) no pertenece a ningún cliente: la comparación con NULL
            // da NULL, no true, así que la fila no pasa el filtro. Fail-closed sin necesidad de un
            // IS NOT NULL explícito, a diferencia de la versión con EXISTS que sí lo llevaba.
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS assets_client_isolation ON ""Assets"";
                DROP POLICY IF EXISTS assets_staff_access ON ""Assets"";

                CREATE POLICY assets_client_isolation ON ""Assets""
                  FOR ALL TO toner_app
                  USING      (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid)
                  WITH CHECK (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid);

                CREATE POLICY assets_staff_access ON ""Assets""
                  FOR ALL TO toner_app_staff
                  USING (true) WITH CHECK (true);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restaura las políticas con EXISTS (la forma previa a esta migración) antes de quitar las
            // columnas de las que dependen las nuevas.
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS assets_client_isolation ON ""Assets"";
                DROP POLICY IF EXISTS assets_staff_access ON ""Assets"";

                CREATE POLICY assets_client_isolation ON ""Assets""
                  FOR ALL TO toner_app
                  USING (
                    ""CurrentClientLocationId"" IS NOT NULL
                    AND EXISTS (
                      SELECT 1 FROM ""ClientLocations"" cl
                      WHERE cl.""Id"" = ""Assets"".""CurrentClientLocationId""
                        AND cl.""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid
                    )
                  )
                  WITH CHECK (
                    ""CurrentClientLocationId"" IS NOT NULL
                    AND EXISTS (
                      SELECT 1 FROM ""ClientLocations"" cl
                      WHERE cl.""Id"" = ""Assets"".""CurrentClientLocationId""
                        AND cl.""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid
                    )
                  );

                CREATE POLICY assets_staff_access ON ""Assets""
                  FOR ALL TO toner_app_staff USING (true) WITH CHECK (true);

                DROP POLICY IF EXISTS service_tickets_client_isolation ON ""ServiceTickets"";
                DROP POLICY IF EXISTS service_tickets_staff_access ON ""ServiceTickets"";

                CREATE POLICY service_tickets_client_isolation ON ""ServiceTickets""
                  FOR ALL TO toner_app
                  USING (
                    EXISTS (
                      SELECT 1 FROM ""ClientLocations"" cl
                      WHERE cl.""Id"" = ""ServiceTickets"".""ClientLocationId""
                        AND cl.""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid
                    )
                  )
                  WITH CHECK (
                    EXISTS (
                      SELECT 1 FROM ""ClientLocations"" cl
                      WHERE cl.""Id"" = ""ServiceTickets"".""ClientLocationId""
                        AND cl.""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid
                    )
                  );

                CREATE POLICY service_tickets_staff_access ON ""ServiceTickets""
                  FOR ALL TO toner_app_staff USING (true) WITH CHECK (true);

                DROP TRIGGER IF EXISTS assets_sync_client_id ON ""Assets"";
                DROP FUNCTION IF EXISTS sync_asset_client_id();
            ");

            migrationBuilder.DropIndex(name: "IX_ServiceTickets_ClientId", table: "ServiceTickets");
            migrationBuilder.DropIndex(name: "IX_Assets_ClientId", table: "Assets");
            migrationBuilder.DropColumn(name: "ClientId", table: "ServiceTickets");
            migrationBuilder.DropColumn(name: "ClientId", table: "Assets");
        }
    }
}
