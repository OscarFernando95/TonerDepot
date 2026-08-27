using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// RLS sobre Clients, la tabla RAÍZ del modelo de tenencia (SECURITY_AUDIT_V2.md hallazgo N3).
    /// Contiene TaxId (NIT), ContactName, ContactEmail y ContactPhone, y era la única tabla a 0 saltos
    /// que seguía sin política: un rol de cliente que llegara a consultarla —hoy no hay endpoint que lo
    /// permita, ClientsController es solo-Staff— vería todos los clientes.
    ///
    /// Es la pieza más barata de todo el trabajo de RLS pendiente: NO requiere denormalizar nada ni
    /// backfill, porque la columna de tenencia ya existe y ya es la PK. Por eso va en su propia
    /// migración y su propio commit, antes que la fase 3a (ServiceTickets/Assets): así no queda
    /// rehén de un trabajo más riesgoso.
    ///
    /// Sin índice nuevo: "Id" es la clave primaria, así que ya tiene un btree único y el predicado
    /// "Id" = ... resuelve por Index Scan sin agregar nada.
    /// </summary>
    public partial class AddClientsRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Misma guarda que SplitRlsPoliciesByRole, y por el mismo motivo, aplicado ahora a una
            // tabla nueva: con FORCE ROW LEVEL SECURITY las políticas aplican TAMBIÉN al owner, y con
            // políticas restringidas por rol un owner que no herede de toner_app_staff no coincide con
            // NINGUNA — sus UPDATE/DELETE sobre Clients afectarían 0 filas EN SILENCIO.
            //
            // No se detecta en desarrollo: en el docker-compose el owner es superusuario y bypasea RLS
            // siempre, así que el modo de fallo solo aparece donde el owner NO lo sea. De ahí que la
            // comprobación viva en la migración y no en un test. Modo 'usage' (no 'member') porque
            // comprueba privilegios HEREDADOS, que es el criterio con el que Postgres empareja TO <rol>.
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
                      'UPDATE/DELETE sobre Clients afectarían 0 filas EN SILENCIO. Corre '
                      'docker/postgres/create-app-role.sh primero.',
                      current_user;
                  END IF;
                END
                $$;
            ");

            // Un Cliente solo alcanza su propia fila. NULLIF convierte la cadena vacía (el valor que
            // setea TenantContextInterceptor cuando no hay cliente) en NULL antes del cast a uuid, y
            // "Id" = NULL da NULL, no true: fail-closed, igual que el resto de las políticas.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Clients"" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ""Clients"" FORCE ROW LEVEL SECURITY;

                CREATE POLICY clients_client_isolation ON ""Clients""
                  FOR ALL TO toner_app
                  USING      (""Id"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid)
                  WITH CHECK (""Id"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid);

                CREATE POLICY clients_staff_access ON ""Clients""
                  FOR ALL TO toner_app_staff
                  USING (true) WITH CHECK (true);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS clients_staff_access ON ""Clients"";
                DROP POLICY IF EXISTS clients_client_isolation ON ""Clients"";
                ALTER TABLE ""Clients"" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE ""Clients"" DISABLE ROW LEVEL SECURITY;
            ");
        }
    }
}
