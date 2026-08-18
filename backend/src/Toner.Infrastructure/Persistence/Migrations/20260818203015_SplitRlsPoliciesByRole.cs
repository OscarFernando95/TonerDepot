using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Parte cada política RLS en dos políticas restringidas por rol, para que el predicado que le
    /// toca al Cliente sea sargable (CODE_QUALITY_AUDIT.md hallazgo #2).
    ///
    /// El problema: mientras el bypass de staff vivía dentro del predicado
    /// ("current_setting('app.is_staff') = 'on' OR ClientId = ..."), Postgres no podía usar el índice
    /// de ClientId. Un OR contra una función STABLE no se pliega en tiempo de planificación, así que
    /// TODA consulta bajo un token de Cliente hacía Seq Scan completo — verificado con EXPLAIN
    /// ANALYZE, y sin que el cast corregido en FixRlsCastForIndexUsage bastara para arreglarlo.
    ///
    /// La corrección: dos políticas por tabla, cada una con TO &lt;rol&gt;. Postgres descarta al
    /// planificar la que no aplica al rol activo, así que al Cliente le queda un predicado limpio y
    /// sargable — Seq Scan (cost 0.00..352.06, 8001 filas descartadas) → Index Scan (cost 0.29..8.31).
    /// Que current_setting() sea STABLE nunca fue el bloqueo: las funciones STABLE sí pueden ser
    /// clave de índice, se evalúan una vez al inicio de la ejecución. El bloqueo era el OR.
    ///
    /// Por qué el rol de staff NO tiene BYPASSRLS: su acceso amplio se expresa como una política
    /// permisiva USING (true) por tabla, visible en pg_policies y auditable, en vez de un atributo de
    /// rol que saltaría en silencio también cualquier tabla que se agregue a RLS en el futuro. Es
    /// además lo que permite que create-app-role.sh siga afirmando que ningún rol de la app tiene
    /// BYPASSRLS.
    ///
    /// ⚠️ El rol toner_app_staff debe existir ANTES de esta migración; se crea en
    /// docker/postgres/create-app-role.sh (un rol es objeto de cluster y su contraseña no debe
    /// versionarse). La aserción de abajo falla ruidosamente si falta.
    /// </summary>
    public partial class SplitRlsPoliciesByRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Guardas previas ──────────────────────────────────────────────────────────────────
            // La segunda es la importante y no es hipotética: las 4 tablas tienen FORCE ROW LEVEL
            // SECURITY, así que las políticas aplican TAMBIÉN al owner. Con políticas restringidas por
            // rol, un owner que no herede de toner_app_staff no coincide con NINGUNA y sus
            // UPDATE/DELETE afectarían 0 filas EN SILENCIO.
            //
            // Y esto no se puede detectar en desarrollo: en el docker-compose el owner es superusuario
            // (bypassea RLS siempre), así que el modo de fallo solo aparece en un entorno donde el
            // owner NO sea superusuario. De ahí que la comprobación viva en la migración y no en un
            // test. Modo 'usage' y no 'member': 'usage' comprueba privilegios HEREDADOS, que es
            // exactamente el criterio con el que Postgres empareja las políticas TO <rol>.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'toner_app_staff') THEN
                    RAISE EXCEPTION
                      'Falta el rol toner_app_staff. Corre docker/postgres/create-app-role.sh contra '
                      'esta base antes de aplicar la migración.';
                  END IF;

                  IF NOT (SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user)
                     AND NOT pg_has_role(current_user, 'toner_app_staff', 'usage') THEN
                    RAISE EXCEPTION
                      'El rol de migraciones (%) no es superusuario ni hereda privilegios de '
                      'toner_app_staff. Con FORCE ROW LEVEL SECURITY las políticas le aplican, y sus '
                      'UPDATE/DELETE sobre ClientLocations/Contracts/ServiceTickets/Assets afectarían '
                      '0 filas EN SILENCIO. Corre docker/postgres/create-app-role.sh primero.',
                      current_user;
                  END IF;
                END
                $$;
            ");

            // ── Privilegios de toner_app_staff (espejo de los de toner_app) ──────────────────────
            migrationBuilder.Sql(@"
                GRANT USAGE ON SCHEMA public TO toner_app_staff;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO toner_app_staff;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO toner_app_staff;

                ALTER DEFAULT PRIVILEGES IN SCHEMA public
                  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO toner_app_staff;
                ALTER DEFAULT PRIVILEGES IN SCHEMA public
                  GRANT USAGE, SELECT ON SEQUENCES TO toner_app_staff;
            ");

            // Mismo criterio que con toner_app: ExceptionLogs guarda stack traces y correos, y la app
            // no tiene ningún caso de uso para LEERLO — solo escribe (TonerExceptionLogger). Quitarle
            // el SELECT también al rol de staff reduce lo que un compromiso puede extraer.
            migrationBuilder.Sql(@"REVOKE SELECT ON ""ExceptionLogs"" FROM toner_app_staff;");

            // ── Políticas por rol ────────────────────────────────────────────────────────────────
            // Tablas con vínculo DIRECTO a Clients: el predicado queda plenamente sargable contra
            // IX_ClientLocations_ClientId / IX_Contracts_ClientId.
            migrationBuilder.Sql(@"
                DROP POLICY client_locations_client_isolation ON ""ClientLocations"";

                CREATE POLICY client_locations_client_isolation ON ""ClientLocations""
                  FOR ALL TO toner_app
                  USING (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid)
                  WITH CHECK (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid);

                CREATE POLICY client_locations_staff_access ON ""ClientLocations""
                  FOR ALL TO toner_app_staff
                  USING (true) WITH CHECK (true);
            ");

            migrationBuilder.Sql(@"
                DROP POLICY contracts_client_isolation ON ""Contracts"";

                CREATE POLICY contracts_client_isolation ON ""Contracts""
                  FOR ALL TO toner_app
                  USING (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid)
                  WITH CHECK (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid);

                CREATE POLICY contracts_staff_access ON ""Contracts""
                  FOR ALL TO toner_app_staff
                  USING (true) WITH CHECK (true);
            ");

            // Tablas a un salto: conservan el EXISTS, solo pierden el OR. Sin el OR el planner puede
            // resolverlo como semi-join con acceso indexado en vez de evaluar el subplan fila por fila
            // sobre la tabla completa.
            migrationBuilder.Sql(@"
                DROP POLICY service_tickets_client_isolation ON ""ServiceTickets"";

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
                  FOR ALL TO toner_app_staff
                  USING (true) WITH CHECK (true);
            ");

            // Assets."CurrentClientLocationId" es NULLABLE y MUTABLE (un equipo vuelve a bodega y se
            // reinstala en otro cliente). El IS NOT NULL explícito documenta que un activo sin sede no
            // pertenece a ningún cliente, y deja al planner descartar esas filas sin evaluar el EXISTS.
            migrationBuilder.Sql(@"
                DROP POLICY assets_client_isolation ON ""Assets"";

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
                  FOR ALL TO toner_app_staff
                  USING (true) WITH CHECK (true);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Vuelve a la política única con el OR de app.is_staff. Ojo: revertir esta migración SIN
            // revertir también el código de TenantContextInterceptor deja a staff sin acceso — el
            // interceptor ya no setea app.is_staff, así que el OR nunca se cumpliría. Los dos van
            // juntos (ver README, "Rollout de un cambio de políticas RLS").
            migrationBuilder.Sql(@"
                DROP POLICY client_locations_client_isolation ON ""ClientLocations"";
                DROP POLICY client_locations_staff_access ON ""ClientLocations"";
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
                DROP POLICY contracts_staff_access ON ""Contracts"";
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
                DROP POLICY service_tickets_staff_access ON ""ServiceTickets"";
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
                DROP POLICY assets_staff_access ON ""Assets"";
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

            // Los GRANT a toner_app_staff no se revocan acá a propósito: el rol es objeto de cluster,
            // creado fuera de las migraciones, y retirarlo es tarea de ops
            // (REVOKE ... ; DROP OWNED BY toner_app_staff; DROP ROLE toner_app_staff).
        }
    }
}
