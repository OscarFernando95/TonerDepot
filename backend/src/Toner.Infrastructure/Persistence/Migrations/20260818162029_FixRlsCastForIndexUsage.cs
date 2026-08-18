using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Corrige el predicado de las 4 políticas RLS existentes (client_locations_client_isolation,
    /// contracts_client_isolation, service_tickets_client_isolation, assets_client_isolation):
    /// comparaban "ClientId"::text contra current_setting(...), y ese cast sobre la COLUMNA hace que
    /// Postgres no pueda usar el índice btree de "ClientId" (tipo uuid) para ESE predicado
    /// (CODE_QUALITY_AUDIT.md hallazgo #2). El fix es castear el SETTING, no la columna:
    /// "ClientId" = NULLIF(current_setting(...), '')::uuid. NULLIF convierte la cadena vacía en NULL
    /// antes del cast (current_client_id se setea como cadena vacía cuando no hay cliente — ver
    /// TenantContextInterceptor), y "ClientId" = NULL da NULL, no true — mismo comportamiento
    /// fail-closed que antes.
    ///
    /// ⚠️ MEJORA PARCIAL, verificada con EXPLAIN ANALYZE contra 8000 filas sintéticas — no eliminar
    /// esta advertencia sin volver a medir. El predicado "ClientId" = X::uuid SÍ es sargable en
    /// aislamiento (Index Scan, cost=0.28..8.30). Pero la política completa sigue evaluando
    /// current_setting('app.is_staff', true) = 'on' OR (predicado de ClientId), y esa función es
    /// STABLE (no IMMUTABLE): Postgres no la resuelve en tiempo de planificación, así que el OR entre
    /// "algo que solo se sabe en ejecución" y "una condición indexable" no genera un plan
    /// condicional — el resultado sigue siendo Seq Scan completo de la tabla (confirmado: dividir en
    /// dos políticas PERMISSIVE en vez de un OR explícito da el mismo plan, Postgres las combina
    /// igual). Esta limitación es estructural de RLS en Postgres con este patrón de política
    /// "bypass OR condición", no un defecto de sintaxis — no se resuelve reformulando el predicado.
    /// service_tickets_client_isolation y assets_client_isolation tienen la misma forma
    /// (is_staff OR EXISTS(...)) y por lo tanto el mismo límite, no verificado por separado pero
    /// inferido de la misma causa.
    ///
    /// El cast sigue siendo la corrección correcta: dejar la columna sargable es un prerequisito para
    /// cualquier solución real (ej. separar a staff a un rol de Postgres con BYPASSRLS, evitando el
    /// OR por completo) y no tiene downside — mismo comportamiento, sin regresión, verificado con la
    /// suite completa de RowLevelSecurityTests. Lo que NO se debe afirmar es que esto por sí solo
    /// convierte las políticas en Index Scan: no lo hace.
    ///
    /// DDL puro (DROP POLICY / CREATE POLICY), no DML — FORCE ROW LEVEL SECURITY no aplica acá, así
    /// que esta migración no necesita `SET LOCAL app.is_staff = 'on'` (a diferencia de un UPDATE/DELETE
    /// sobre estas tablas — ver README, sección "RLS y migraciones").
    /// </summary>
    public partial class FixRlsCastForIndexUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY client_locations_client_isolation ON ""ClientLocations"";
                CREATE POLICY client_locations_client_isolation ON ""ClientLocations""
                  FOR ALL
                  USING (
                    current_setting('app.is_staff', true) = 'on'
                    OR ""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid
                  )
                  WITH CHECK (
                    current_setting('app.is_staff', true) = 'on'
                    OR ""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid
                  );
            ");

            migrationBuilder.Sql(@"
                DROP POLICY contracts_client_isolation ON ""Contracts"";
                CREATE POLICY contracts_client_isolation ON ""Contracts""
                  FOR ALL
                  USING (
                    current_setting('app.is_staff', true) = 'on'
                    OR ""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid
                  )
                  WITH CHECK (
                    current_setting('app.is_staff', true) = 'on'
                    OR ""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid
                  );
            ");

            migrationBuilder.Sql(@"
                DROP POLICY service_tickets_client_isolation ON ""ServiceTickets"";
                CREATE POLICY service_tickets_client_isolation ON ""ServiceTickets""
                  FOR ALL
                  USING (
                    current_setting('app.is_staff', true) = 'on'
                    OR EXISTS (
                      SELECT 1 FROM ""ClientLocations"" cl
                      WHERE cl.""Id"" = ""ServiceTickets"".""ClientLocationId""
                        AND cl.""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid
                    )
                  )
                  WITH CHECK (
                    current_setting('app.is_staff', true) = 'on'
                    OR EXISTS (
                      SELECT 1 FROM ""ClientLocations"" cl
                      WHERE cl.""Id"" = ""ServiceTickets"".""ClientLocationId""
                        AND cl.""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid
                    )
                  );
            ");

            migrationBuilder.Sql(@"
                DROP POLICY assets_client_isolation ON ""Assets"";
                CREATE POLICY assets_client_isolation ON ""Assets""
                  FOR ALL
                  USING (
                    current_setting('app.is_staff', true) = 'on'
                    OR (
                      ""CurrentClientLocationId"" IS NOT NULL
                      AND EXISTS (
                        SELECT 1 FROM ""ClientLocations"" cl
                        WHERE cl.""Id"" = ""Assets"".""CurrentClientLocationId""
                          AND cl.""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid
                      )
                    )
                  )
                  WITH CHECK (
                    current_setting('app.is_staff', true) = 'on'
                    OR (
                      ""CurrentClientLocationId"" IS NOT NULL
                      AND EXISTS (
                        SELECT 1 FROM ""ClientLocations"" cl
                        WHERE cl.""Id"" = ""Assets"".""CurrentClientLocationId""
                          AND cl.""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid
                      )
                    )
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY client_locations_client_isolation ON ""ClientLocations"";
                CREATE POLICY client_locations_client_isolation ON ""ClientLocations""
                  FOR ALL
                  USING (
                    current_setting('app.is_staff', true) = 'on'
                    OR ""ClientId""::text = current_setting('app.current_client_id', true)
                  )
                  WITH CHECK (
                    current_setting('app.is_staff', true) = 'on'
                    OR ""ClientId""::text = current_setting('app.current_client_id', true)
                  );
            ");

            migrationBuilder.Sql(@"
                DROP POLICY contracts_client_isolation ON ""Contracts"";
                CREATE POLICY contracts_client_isolation ON ""Contracts""
                  FOR ALL
                  USING (
                    current_setting('app.is_staff', true) = 'on'
                    OR ""ClientId""::text = current_setting('app.current_client_id', true)
                  )
                  WITH CHECK (
                    current_setting('app.is_staff', true) = 'on'
                    OR ""ClientId""::text = current_setting('app.current_client_id', true)
                  );
            ");

            migrationBuilder.Sql(@"
                DROP POLICY service_tickets_client_isolation ON ""ServiceTickets"";
                CREATE POLICY service_tickets_client_isolation ON ""ServiceTickets""
                  FOR ALL
                  USING (
                    current_setting('app.is_staff', true) = 'on'
                    OR EXISTS (
                      SELECT 1 FROM ""ClientLocations"" cl
                      WHERE cl.""Id"" = ""ServiceTickets"".""ClientLocationId""
                        AND cl.""ClientId""::text = current_setting('app.current_client_id', true)
                    )
                  )
                  WITH CHECK (
                    current_setting('app.is_staff', true) = 'on'
                    OR EXISTS (
                      SELECT 1 FROM ""ClientLocations"" cl
                      WHERE cl.""Id"" = ""ServiceTickets"".""ClientLocationId""
                        AND cl.""ClientId""::text = current_setting('app.current_client_id', true)
                    )
                  );
            ");

            migrationBuilder.Sql(@"
                DROP POLICY assets_client_isolation ON ""Assets"";
                CREATE POLICY assets_client_isolation ON ""Assets""
                  FOR ALL
                  USING (
                    current_setting('app.is_staff', true) = 'on'
                    OR (
                      ""CurrentClientLocationId"" IS NOT NULL
                      AND EXISTS (
                        SELECT 1 FROM ""ClientLocations"" cl
                        WHERE cl.""Id"" = ""Assets"".""CurrentClientLocationId""
                          AND cl.""ClientId""::text = current_setting('app.current_client_id', true)
                      )
                    )
                  )
                  WITH CHECK (
                    current_setting('app.is_staff', true) = 'on'
                    OR (
                      ""CurrentClientLocationId"" IS NOT NULL
                      AND EXISTS (
                        SELECT 1 FROM ""ClientLocations"" cl
                        WHERE cl.""Id"" = ""Assets"".""CurrentClientLocationId""
                          AND cl.""ClientId""::text = current_setting('app.current_client_id', true)
                      )
                    )
                  );
            ");
        }
    }
}
