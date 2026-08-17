using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Cierra el último hueco de RLS en la superficie del portal de clientes (SECURITY_AUDIT_V2.md
    /// hallazgo N1). De las cuatro tablas que el rol Cliente puede alcanzar, tres recibieron política
    /// en AddRowLevelSecurity (ClientLocations, Contracts, ServiceTickets) y Assets quedó fuera pese a
    /// estar a un salto — la misma forma que ServiceTickets. Tampoco estaba en la lista de fase 3
    /// diferida (tablas a 2-3 saltos), así que se había caído entre las dos fases.
    ///
    /// Sin esto, el aislamiento entre clientes en GET /api/assets y /api/assets/{id} dependía
    /// exclusivamente del Where de C# — justamente lo que RLS viene a respaldar como segunda capa.
    /// El filtrado en C# (AssetService) sigue intacto y sigue siendo la primera línea.
    /// </summary>
    public partial class AddAssetsRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Assets."CurrentClientLocationId" es NULLABLE (activo en bodega o dado de baja) y además
            // MUTABLE (un equipo vuelve a bodega y se reinstala en otro cliente). El EXISTS por sí
            // solo ya denegaría con NULL — cl."Id" = NULL da NULL, la subconsulta no devuelve filas y
            // EXISTS es false — pero el IS NOT NULL se deja explícito por dos razones: documenta la
            // intención (un activo sin sede instalada no pertenece a ningún cliente) y permite al
            // planner descartar esas filas sin evaluar la subconsulta.
            //
            // ⚠️ FORCE ROW LEVEL SECURITY: como en las otras tres tablas, las políticas aplican
            // TAMBIÉN al owner. Cualquier migración futura con UPDATE/DELETE sobre "Assets" debe
            // abrir con `SET LOCAL app.is_staff = 'on';` o afectará 0 filas EN SILENCIO. Esto incluye
            // las migraciones históricas que hacen DELETE FROM "Assets"
            // (RedesignMaintenanceForBrandModelThresholds): si se re-ejecutaran desde cero contra una
            // base nueva cuyo owner NO sea superusuario, quedarían sujetas a esta política. En el
            // docker-compose de desarrollo el owner sí es superusuario, así que ahí no se reproduce.
            // Ver README.md, sección "RLS y migraciones".
            migrationBuilder.Sql(@"
                ALTER TABLE ""Assets"" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ""Assets"" FORCE ROW LEVEL SECURITY;
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS assets_client_isolation ON ""Assets"";
                ALTER TABLE ""Assets"" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE ""Assets"" DISABLE ROW LEVEL SECURITY;
            ");
        }
    }
}
