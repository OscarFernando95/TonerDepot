using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Sedes permitidas por usuario Cliente (UserClientLocations) y la bandera Users.RestrictedToLocations.
    ///
    /// La tabla lleva ClientId capturado al escribir desde la sede y su propia política RLS (regla del proyecto: toda tabla
    /// alcanzable desde Clients, sin excepciones). Predicado con ::uuid (nunca ::text) y políticas separadas por rol, como
    /// en SplitRlsPoliciesByRole. RLS aísla por EMPRESA; que un cliente vea solo algunas sedes de la suya lo hace cumplir la
    /// aplicación (ClientScope) — decisión registrada: la frontera de seguridad entre clientes sigue siendo RLS.
    /// </summary>
    public partial class AddUserClientLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RestrictedToLocations",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "UserClientLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClientLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClientLocations_ClientLocations_ClientLocationId",
                        column: x => x.ClientLocationId,
                        principalTable: "ClientLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserClientLocations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserClientLocations_ClientId",
                table: "UserClientLocations",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_UserClientLocations_ClientLocationId",
                table: "UserClientLocations",
                column: "ClientLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_UserClientLocations_UserId_ClientLocationId",
                table: "UserClientLocations",
                columns: new[] { "UserId", "ClientLocationId" },
                unique: true);

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

                ALTER TABLE ""UserClientLocations"" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ""UserClientLocations"" FORCE ROW LEVEL SECURITY;

                CREATE POLICY userclientlocations_client_isolation ON ""UserClientLocations""
                  FOR ALL TO toner_app
                  USING (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid)
                  WITH CHECK (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid);

                CREATE POLICY userclientlocations_staff_access ON ""UserClientLocations""
                  FOR ALL TO toner_app_staff
                  USING (true) WITH CHECK (true);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS userclientlocations_client_isolation ON ""UserClientLocations"";
                DROP POLICY IF EXISTS userclientlocations_staff_access ON ""UserClientLocations"";
            ");

            migrationBuilder.DropTable(
                name: "UserClientLocations");

            migrationBuilder.DropColumn(
                name: "RestrictedToLocations",
                table: "Users");
        }
    }
}
