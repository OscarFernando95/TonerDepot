using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// RLS para TechnicianAssets (vínculo técnico-activo), tabla que nació sin política en AddTechnicianAssets.
    /// SECURITY_AUDIT_V2.md hallazgo N13.
    ///
    /// A diferencia de las tablas de la fase 3b, aquí NO se denormaliza ClientId: el vínculo es un ESTADO
    /// ACTUAL (el administrador lo crea y lo quita), no un hecho histórico, y el activo puede cambiar de
    /// cliente. Una copia capturada al escribir quedaría vieja tras el traslado y le seguiría mostrando al
    /// cliente anterior qué técnico atiende un equipo que ya no es suyo. La política consulta Assets."ClientId",
    /// que ya mantiene sincronizado el trigger assets_sync_client_id, así que siempre refleja la sede actual.
    ///
    /// El predicado sigue las reglas del proyecto: comparación con ::uuid (nunca ::text, que vuelve el
    /// predicado no-sargable) y políticas separadas por rol. Un activo en bodega tiene ClientId NULL: la
    /// comparación da NULL, no true, y ningún cliente ve ese vínculo (fail-closed).
    /// </summary>
    public partial class AddTechnicianAssetsRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

                ALTER TABLE ""TechnicianAssets"" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ""TechnicianAssets"" FORCE ROW LEVEL SECURITY;

                CREATE POLICY technicianassets_client_isolation ON ""TechnicianAssets""
                  FOR ALL TO toner_app
                  USING (EXISTS (
                    SELECT 1 FROM ""Assets"" a
                    WHERE a.""Id"" = ""TechnicianAssets"".""AssetId""
                      AND a.""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid))
                  WITH CHECK (EXISTS (
                    SELECT 1 FROM ""Assets"" a
                    WHERE a.""Id"" = ""TechnicianAssets"".""AssetId""
                      AND a.""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid));

                CREATE POLICY technicianassets_staff_access ON ""TechnicianAssets""
                  FOR ALL TO toner_app_staff
                  USING (true) WITH CHECK (true);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS technicianassets_client_isolation ON ""TechnicianAssets"";
                DROP POLICY IF EXISTS technicianassets_staff_access ON ""TechnicianAssets"";
                ALTER TABLE ""TechnicianAssets"" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE ""TechnicianAssets"" DISABLE ROW LEVEL SECURITY;
            ");
        }
    }
}
