using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ZoneId",
                table: "Cities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Zones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TechnicianZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianZones_Technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TechnicianZones_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cities_ZoneId",
                table: "Cities",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianZones_TechnicianId_ZoneId",
                table: "TechnicianZones",
                columns: new[] { "TechnicianId", "ZoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianZones_ZoneId",
                table: "TechnicianZones",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Zones_Name",
                table: "Zones",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cities_Zones_ZoneId",
                table: "Cities",
                column: "ZoneId",
                principalTable: "Zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ── Conversión de datos ──────────────────────────────────────────────────────────────
            // Hasta hoy la cobertura era técnico-ciudad. Cada ciudad que algún técnico cubre se convierte en una zona
            // (con el nombre de la ciudad; si el nombre se repite entre departamentos, se le agrega el departamento)
            // y cada técnico queda asignado a las zonas de sus ciudades. Así nada cambia para la asignación de
            // trabajo, y después se pueden reagrupar a mano. Debe correr ANTES de borrar TechnicianCoverages.
            migrationBuilder.Sql(@"
                CREATE TEMP TABLE zone_map ON COMMIT DROP AS
                SELECT c.""Id"" AS city_id,
                       gen_random_uuid() AS zone_id,
                       CASE WHEN count(*) OVER (PARTITION BY c.""Name"") > 1
                            THEN c.""Name"" || ' (' || c.""StateOrProvince"" || ')'
                            ELSE c.""Name"" END AS zone_name
                FROM ""Cities"" c
                WHERE EXISTS (SELECT 1 FROM ""TechnicianCoverages"" tc WHERE tc.""CityId"" = c.""Id"");

                INSERT INTO ""Zones"" (""Id"", ""Name"", ""CreatedAt"")
                SELECT zone_id, zone_name, now() FROM zone_map;

                UPDATE ""Cities"" c SET ""ZoneId"" = m.zone_id
                FROM zone_map m WHERE m.city_id = c.""Id"";

                INSERT INTO ""TechnicianZones"" (""Id"", ""TechnicianId"", ""ZoneId"", ""CreatedAt"")
                SELECT gen_random_uuid(), tc.""TechnicianId"", m.zone_id, now()
                FROM ""TechnicianCoverages"" tc JOIN zone_map m ON m.city_id = tc.""CityId"";
            ");

            migrationBuilder.DropTable(
                name: "TechnicianCoverages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TechnicianCoverages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianCoverages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianCoverages_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicianCoverages_Technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianCoverages_CityId",
                table: "TechnicianCoverages",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianCoverages_TechnicianId_CityId",
                table: "TechnicianCoverages",
                columns: new[] { "TechnicianId", "CityId" },
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO ""TechnicianCoverages"" (""Id"", ""TechnicianId"", ""CityId"", ""CreatedAt"")
                SELECT gen_random_uuid(), tz.""TechnicianId"", c.""Id"", now()
                FROM ""TechnicianZones"" tz JOIN ""Cities"" c ON c.""ZoneId"" = tz.""ZoneId""
                ON CONFLICT DO NOTHING;
            ");


            migrationBuilder.DropForeignKey(
                name: "FK_Cities_Zones_ZoneId",
                table: "Cities");

            migrationBuilder.DropTable(
                name: "TechnicianZones");

            migrationBuilder.DropTable(
                name: "Zones");

            migrationBuilder.DropIndex(
                name: "IX_Cities_ZoneId",
                table: "Cities");

            migrationBuilder.DropColumn(
                name: "ZoneId",
                table: "Cities");
        }
    }
}
