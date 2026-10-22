using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Hilo de comentarios y programación de las órdenes de mantenimiento (MaintenanceOrderComments) y las columnas
    /// MaintenanceOrders.PlannedAt / LastCommentAt.
    ///
    /// La tabla lleva ClientId capturado al escribir desde la orden y su propia política RLS (regla del proyecto: toda tabla
    /// alcanzable desde Clients, sin excepciones). Predicado con ::uuid (nunca ::text) y políticas separadas por rol, como en
    /// SplitRlsPoliciesByRole. El técnico opera con el contexto de staff (hallazgo N8): que solo escriba en SUS órdenes lo
    /// hace cumplir el servicio.
    /// </summary>
    public partial class AddMaintenanceOrderComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastCommentAt",
                table: "MaintenanceOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedAt",
                table: "MaintenanceOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaintenanceOrderComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenanceOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorRole = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PlannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceOrderComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceOrderComments_MaintenanceOrders_MaintenanceOrder~",
                        column: x => x.MaintenanceOrderId,
                        principalTable: "MaintenanceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceOrderComments_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrderComments_AuthorUserId",
                table: "MaintenanceOrderComments",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrderComments_ClientId",
                table: "MaintenanceOrderComments",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrderComments_MaintenanceOrderId_CreatedAt",
                table: "MaintenanceOrderComments",
                columns: new[] { "MaintenanceOrderId", "CreatedAt" });

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

                ALTER TABLE ""MaintenanceOrderComments"" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ""MaintenanceOrderComments"" FORCE ROW LEVEL SECURITY;

                CREATE POLICY maintenanceordercomments_client_isolation ON ""MaintenanceOrderComments""
                  FOR ALL TO toner_app
                  USING (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid)
                  WITH CHECK (""ClientId"" = NULLIF(current_setting('app.current_client_id', true), '')::uuid);

                CREATE POLICY maintenanceordercomments_staff_access ON ""MaintenanceOrderComments""
                  FOR ALL TO toner_app_staff
                  USING (true) WITH CHECK (true);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS maintenanceordercomments_client_isolation ON ""MaintenanceOrderComments"";
                DROP POLICY IF EXISTS maintenanceordercomments_staff_access ON ""MaintenanceOrderComments"";
            ");

            migrationBuilder.DropTable(
                name: "MaintenanceOrderComments");

            migrationBuilder.DropColumn(
                name: "LastCommentAt",
                table: "MaintenanceOrders");

            migrationBuilder.DropColumn(
                name: "PlannedAt",
                table: "MaintenanceOrders");
        }
    }
}
