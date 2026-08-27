using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Fase 3b de RLS: extiende la cobertura a las 7 tablas que quedaban sin política, con el mismo
    /// patrón ya probado en 3a — columna ClientId denormalizada + índice + dos políticas partidas por
    /// rol. Cierra el trabajo de RLS pendiente desde SECURITY_AUDIT.md hallazgo #5.
    ///
    /// Diferencia de alcance con 3a: aquellas dos tablas YA tenían política (con EXISTS) y el trabajo
    /// era de rendimiento. Estas siete no tenían ninguna, así que esto es COBERTURA — defensa en
    /// profundidad. Hoy ningún endpoint alcanzable por un Cliente las toca directamente; la política
    /// existe para que un endpoint futuro no las exponga sin red debajo.
    ///
    /// Todas capturan al escribir (ninguna es un estado actual como Assets, así que ninguna necesita
    /// trigger de sincronización). Evidences queda FUERA a propósito: no tiene ningún camino de
    /// escritura en el código, se hará junto con la funcionalidad de subida (hallazgo #23).
    ///
    /// Nulabilidad, decidida caso por caso:
    ///   NOT NULL → MaintenanceSchedules, MaintenanceOrders, ContractAssets, AssignmentHistories.
    ///              Todas nacen de un padre que siempre tiene cliente.
    ///   NULL     → MeterReadings, AssetStatusLogs, TimeLogs. Pueden referirse a un activo en bodega,
    ///              que no pertenece a nadie. NULL no lo ve ningún cliente: fail-closed.
    /// </summary>
    public partial class AddPhase3bDenormalizedClientId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Misma guarda que las demás migraciones de RLS: el backfill es DML, y aunque estas tablas
            // todavía no tengan FORCE RLS en este punto, sí lo tendrán al final de esta misma
            // migración. Se comprueba antes para fallar temprano y con un mensaje claro.
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
                      'El rol de migraciones (%) no es superusuario ni hereda de toner_app_staff. Con '
                      'FORCE ROW LEVEL SECURITY su DML afectaría 0 filas EN SILENCIO. Corre '
                      'docker/postgres/create-app-role.sh primero.', current_user;
                  END IF;
                END
                $$;
            ");

            // ── Columnas: todas nullable al principio, el backfill va después ────────────────────
            foreach (var table in new[]
            {
                "MeterReadings", "MaintenanceOrders", "MaintenanceSchedules",
                "AssetStatusLogs", "ContractAssets", "TimeLogs", "AssignmentHistories"
            })
            {
                migrationBuilder.AddColumn<Guid>(name: "ClientId", table: table, type: "uuid", nullable: true);
            }

            // ── Backfill ─────────────────────────────────────────────────────────────────────────
            // El orden importa: MaintenanceSchedules antes que MaintenanceOrders (que deriva de él), y
            // ambos antes que TimeLogs (que puede derivar de una orden). Cada uno usa la fuente estable
            // que corresponde a su semántica, no siempre el activo.
            //
            // Nota: hoy las siete tablas están vacías en desarrollo, así que esto es un no-evento. Se
            // escribe igual para que la migración sea correcta contra cualquier base que sí tenga datos.

            // Desde el contrato (ClientId inmutable).
            migrationBuilder.Sql(@"
                UPDATE ""MaintenanceSchedules"" s SET ""ClientId"" = c.""ClientId""
                FROM ""Contracts"" c WHERE c.""Id"" = s.""ContractId"" AND s.""ClientId"" IS NULL;

                UPDATE ""ContractAssets"" ca SET ""ClientId"" = c.""ClientId""
                FROM ""Contracts"" c WHERE c.""Id"" = ca.""ContractId"" AND ca.""ClientId"" IS NULL;
            ");

            // Desde el cronograma que la generó.
            migrationBuilder.Sql(@"
                UPDATE ""MaintenanceOrders"" o SET ""ClientId"" = s.""ClientId""
                FROM ""MaintenanceSchedules"" s WHERE s.""Id"" = o.""MaintenanceScheduleId"" AND o.""ClientId"" IS NULL;
            ");

            // Desde el activo (denormalizado y mantenido por trigger desde la fase 3a). Para filas
            // históricas esto usa la sede ACTUAL del activo: si un equipo ya cambió de cliente, las
            // lecturas viejas quedarían atribuidas al cliente nuevo. Hoy no aplica —las tablas están
            // vacías— y el esquema no guarda el histórico de sedes, así que no hay forma de
            // reconstruirlo. Queda anotado por si alguna vez se hace un backfill sobre datos reales.
            migrationBuilder.Sql(@"
                UPDATE ""MeterReadings"" mr SET ""ClientId"" = a.""ClientId""
                FROM ""Assets"" a WHERE a.""Id"" = mr.""AssetId"" AND mr.""ClientId"" IS NULL;

                UPDATE ""AssetStatusLogs"" l SET ""ClientId"" = a.""ClientId""
                FROM ""Assets"" a WHERE a.""Id"" = l.""AssetId"" AND l.""ClientId"" IS NULL;
            ");

            // Desde el padre presente (exactamente uno, por el check constraint de cada tabla).
            migrationBuilder.Sql(@"
                UPDATE ""AssignmentHistories"" h SET ""ClientId"" = t.""ClientId""
                FROM ""ServiceTickets"" t WHERE t.""Id"" = h.""ServiceTicketId"" AND h.""ClientId"" IS NULL;

                UPDATE ""AssignmentHistories"" h SET ""ClientId"" = o.""ClientId""
                FROM ""MaintenanceOrders"" o WHERE o.""Id"" = h.""MaintenanceOrderId"" AND h.""ClientId"" IS NULL;

                UPDATE ""TimeLogs"" tl SET ""ClientId"" = t.""ClientId""
                FROM ""ServiceTickets"" t WHERE t.""Id"" = tl.""ServiceTicketId"" AND tl.""ClientId"" IS NULL;

                UPDATE ""TimeLogs"" tl SET ""ClientId"" = o.""ClientId""
                FROM ""MaintenanceOrders"" o WHERE o.""Id"" = tl.""MaintenanceOrderId"" AND tl.""ClientId"" IS NULL;

                UPDATE ""TimeLogs"" tl SET ""ClientId"" = a.""ClientId""
                FROM ""Assets"" a WHERE a.""Id"" = tl.""AssetId"" AND tl.""ClientId"" IS NULL;
            ");

            // Aborta con un mensaje entendible en vez de dejar que reviente el NOT NULL de abajo. Solo
            // sobre las cuatro tablas que sí deben quedar completas; en las otras tres NULL es válido.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE t text; n int;
                BEGIN
                  FOREACH t IN ARRAY ARRAY['MaintenanceSchedules','MaintenanceOrders','ContractAssets','AssignmentHistories']
                  LOOP
                    EXECUTE format('SELECT count(*) FROM %I WHERE ""ClientId"" IS NULL', t) INTO n;
                    IF n > 0 THEN
                      RAISE EXCEPTION
                        'Backfill incompleto: % filas de %I sin ClientId derivable de su padre. '
                        'Revisa si hay referencias huérfanas antes de reintentar.', n, t;
                    END IF;
                  END LOOP;
                END
                $$;
            ");

            foreach (var table in new[] { "MaintenanceSchedules", "MaintenanceOrders", "ContractAssets", "AssignmentHistories" })
            {
                migrationBuilder.AlterColumn<Guid>(name: "ClientId", table: table, type: "uuid", nullable: false);
            }

            // ── Índices ──────────────────────────────────────────────────────────────────────────
            foreach (var table in new[]
            {
                "MeterReadings", "MaintenanceOrders", "MaintenanceSchedules",
                "AssetStatusLogs", "ContractAssets", "TimeLogs", "AssignmentHistories"
            })
            {
                migrationBuilder.CreateIndex(name: $"IX_{table}_ClientId", table: table, column: "ClientId");
            }

            // ── Políticas ────────────────────────────────────────────────────────────────────────
            // Comparación directa sobre la columna propia, partida por rol. Un ClientId NULL (activo en
            // bodega) da NULL al compararse, no true, así que no pasa el filtro: fail-closed sin
            // necesidad de un IS NOT NULL explícito.
            foreach (var table in new[]
            {
                "MeterReadings", "MaintenanceOrders", "MaintenanceSchedules",
                "AssetStatusLogs", "ContractAssets", "TimeLogs", "AssignmentHistories"
            })
            {
                var policyPrefix = table.ToLowerInvariant();
                migrationBuilder.Sql($@"
                    ALTER TABLE ""{table}"" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE ""{table}"" FORCE ROW LEVEL SECURITY;

                    CREATE POLICY {policyPrefix}_client_isolation ON ""{table}""
                      FOR ALL TO toner_app
                      USING      (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid)
                      WITH CHECK (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid);

                    CREATE POLICY {policyPrefix}_staff_access ON ""{table}""
                      FOR ALL TO toner_app_staff
                      USING (true) WITH CHECK (true);
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[]
            {
                "MeterReadings", "MaintenanceOrders", "MaintenanceSchedules",
                "AssetStatusLogs", "ContractAssets", "TimeLogs", "AssignmentHistories"
            })
            {
                var policyPrefix = table.ToLowerInvariant();
                migrationBuilder.Sql($@"
                    DROP POLICY IF EXISTS {policyPrefix}_client_isolation ON ""{table}"";
                    DROP POLICY IF EXISTS {policyPrefix}_staff_access ON ""{table}"";
                    ALTER TABLE ""{table}"" NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE ""{table}"" DISABLE ROW LEVEL SECURITY;
                ");

                migrationBuilder.DropIndex(name: $"IX_{table}_ClientId", table: table);
                migrationBuilder.DropColumn(name: "ClientId", table: table);
            }
        }
    }
}
