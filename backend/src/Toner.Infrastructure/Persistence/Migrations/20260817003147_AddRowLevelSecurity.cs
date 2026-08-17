using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Row Level Security como segunda capa de aislamiento por cliente (SECURITY_AUDIT.md hallazgo #5).
    /// Fases 1 y 2: rol sin privilegios de owner + políticas sobre las tablas con vínculo directo o a
    /// un salto de Clients. La fase 3 (denormalizar ClientId en las tablas hoja) queda fuera.
    ///
    /// El rol toner_app se crea FUERA de esta migración (docker/postgres/create-app-role.sh): un rol es
    /// objeto de cluster, no de base de datos, y su contraseña no debe quedar versionada en git — que es
    /// exactamente el hallazgo #1 que se purgó del historial. Si el rol no existe, esta migración falla
    /// ruidosamente, que es el comportamiento correcto.
    /// </summary>
    public partial class AddRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Privilegios de toner_app sobre el esquema público ────────────────────────────
            migrationBuilder.Sql(@"
                GRANT USAGE ON SCHEMA public TO toner_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO toner_app;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO toner_app;
            ");

            // Sin esto, cada migración futura crearía tablas que toner_app no puede leer hasta que
            // alguien corra un GRANT a mano: la app arrancaría y fallaría recién en runtime.
            migrationBuilder.Sql(@"
                ALTER DEFAULT PRIVILEGES IN SCHEMA public
                  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO toner_app;
                ALTER DEFAULT PRIVILEGES IN SCHEMA public
                  GRANT USAGE, SELECT ON SEQUENCES TO toner_app;
            ");

            // ExceptionLogs guarda stack traces completos y correos de usuario (hallazgo #14, abierto).
            // La app necesita escribir ahí, pero no tiene ningún caso de uso para leerlo: quitarle el
            // SELECT reduce lo que un compromiso de la aplicación puede extraer.
            migrationBuilder.Sql(@"REVOKE SELECT ON ""ExceptionLogs"" FROM toner_app;");

            // ── 2. Esquema de Hangfire (opción H3 del diseño) ───────────────────────────────────
            // Hangfire mantiene su propio esquema con 21 scripts incrementales versionados, así que
            // congelarlo en una migración lo rompería en el próximo upgrade del paquete. En vez de eso
            // se le da a toner_app privilegio de CREATE acotado al esquema 'hangfire' y se deja que
            // Hangfire se automantenga (PrepareSchemaIfNecessary sigue en su valor por defecto).
            //
            // Qué preserva esto: toner_app NO es owner de las tablas de 'public', así que no puede
            // hacer ALTER TABLE ... DISABLE ROW LEVEL SECURITY ni DROP sobre datos de negocio. El DDL
            // que sí tiene queda confinado a un esquema que solo contiene la cola de jobs.
            migrationBuilder.Sql(@"
                CREATE SCHEMA IF NOT EXISTS hangfire;
                GRANT USAGE, CREATE ON SCHEMA hangfire TO toner_app;
            ");

            // ── 3. Políticas RLS: aislamiento por cliente ───────────────────────────────────────
            // Alcance deliberado: solo el borde Cliente. Administrador/Coordinador/Tecnico son
            // confiables a nivel de BD (app.is_staff = 'on') porque ven datos de varios clientes por
            // diseño; su filtrado fino sigue en C# y NO se toca. RLS es defensa en profundidad.
            //
            // current_setting(..., true) devuelve NULL si la variable no está seteada, y comparar
            // contra NULL da NULL (no true) => la fila no pasa el filtro. Fail-closed por construcción.
            //
            // ⚠️ FORCE ROW LEVEL SECURITY: las políticas aplican TAMBIÉN al owner. Cualquier migración
            // futura con UPDATE/DELETE sobre estas tres tablas DEBE empezar con
            //     SET LOCAL app.is_staff = 'on';
            // dentro del mismo migrationBuilder.Sql(), o afectará 0 filas EN SILENCIO.
            // Ver README.md, sección "RLS y migraciones".
            migrationBuilder.Sql(@"
                ALTER TABLE ""ClientLocations"" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ""ClientLocations"" FORCE ROW LEVEL SECURITY;
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
                ALTER TABLE ""Contracts"" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ""Contracts"" FORCE ROW LEVEL SECURITY;
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

            // Única tabla a un salto en esta fase. ClientLocations.""Id"" es PK, así que el EXISTS
            // resuelve por index scan.
            migrationBuilder.Sql(@"
                ALTER TABLE ""ServiceTickets"" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ""ServiceTickets"" FORCE ROW LEVEL SECURITY;
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS service_tickets_client_isolation ON ""ServiceTickets"";
                ALTER TABLE ""ServiceTickets"" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE ""ServiceTickets"" DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS contracts_client_isolation ON ""Contracts"";
                ALTER TABLE ""Contracts"" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE ""Contracts"" DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS client_locations_client_isolation ON ""ClientLocations"";
                ALTER TABLE ""ClientLocations"" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE ""ClientLocations"" DISABLE ROW LEVEL SECURITY;
            ");

            // Los GRANT no se revocan aquí a propósito: revocarlos dejaría a la aplicación sin acceso a
            // la base aunque el rollback fuera exitoso. Retirar el rol es tarea de ops
            // (DROP OWNED BY toner_app; DROP ROLE toner_app), no de una migración.
        }
    }
}
