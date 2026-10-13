using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TechnicianAssets.Source: distingue el vínculo hecho a mano (Manual) del derivado de la zona del técnico
    /// (Zone), para poder REVOCAR solo los automáticos cuando cambia la cobertura. Revisión del PR #1.
    ///
    /// Los vínculos existentes quedan como Manual (default de la columna): nunca se revocan solos, que es lo
    /// seguro — hasta hoy ninguno se retiraba por cambios de zona.
    ///
    /// Incluye el backfill que el PR #1 dejó fuera: el autovínculo por zona solo se disparaba al ASIGNAR una zona
    /// o al instalar, así que los técnicos con zona ya asignada y los activos ya instalados no quedaban
    /// vinculados. Se vincula una sola vez a todos los activos Instalado de la zona de cada técnico, como Zone.
    /// Es idempotente (ON CONFLICT DO NOTHING sobre el índice único TechnicianId+AssetId): un vínculo Manual que
    /// ya existía no se degrada a Zone.
    /// </summary>
    public partial class AddTechnicianAssetSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Guarda ───────────────────────────────────────────────────────────────────────────
            // El backfill hace DML sobre tablas con FORCE ROW LEVEL SECURITY y políticas por rol. Un rol de
            // migraciones sin toner_app_staff afectaría 0 filas EN SILENCIO (mismo caso que la fase 3a).
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

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "TechnicianAssets",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Manual");

            // ── Backfill ─────────────────────────────────────────────────────────────────────────
            // Mismo criterio que el código (TechnicianAssetLinks): activo Instalado cuya sede está en un
            // municipio de una zona asignada al técnico.
            migrationBuilder.Sql(@"
                INSERT INTO ""TechnicianAssets"" (""Id"", ""TechnicianId"", ""AssetId"", ""CreatedAt"", ""Source"")
                SELECT gen_random_uuid(), tz.""TechnicianId"", a.""Id"", now(), 'Zone'
                FROM ""Assets"" a
                JOIN ""ClientLocations"" cl ON cl.""Id"" = a.""CurrentClientLocationId""
                JOIN ""Cities"" c ON c.""Id"" = cl.""CityId""
                JOIN ""TechnicianZones"" tz ON tz.""ZoneId"" = c.""ZoneId""
                WHERE a.""LifecycleStatus"" = 'Instalado'
                ON CONFLICT (""TechnicianId"", ""AssetId"") DO NOTHING;
            ");

            // Verificación: si alguna política dejó pasar solo parte de las filas, aborta en vez de dejar
            // técnicos a medias sin que nadie lo note.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE faltantes int;
                BEGIN
                  SELECT count(*) INTO faltantes
                  FROM ""Assets"" a
                  JOIN ""ClientLocations"" cl ON cl.""Id"" = a.""CurrentClientLocationId""
                  JOIN ""Cities"" c ON c.""Id"" = cl.""CityId""
                  JOIN ""TechnicianZones"" tz ON tz.""ZoneId"" = c.""ZoneId""
                  WHERE a.""LifecycleStatus"" = 'Instalado'
                    AND NOT EXISTS (
                      SELECT 1 FROM ""TechnicianAssets"" ta
                      WHERE ta.""TechnicianId"" = tz.""TechnicianId"" AND ta.""AssetId"" = a.""Id"");
                  IF faltantes > 0 THEN
                    RAISE EXCEPTION
                      'Backfill incompleto: % vínculos técnico-activo por zona no se crearon.', faltantes;
                  END IF;
                END
                $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin la columna los automáticos serían indistinguibles de los manuales y quedarían como
            // permisos permanentes: se retiran antes de borrarla.
            migrationBuilder.Sql(@"DELETE FROM ""TechnicianAssets"" WHERE ""Source"" = 'Zone';");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "TechnicianAssets");
        }
    }
}
