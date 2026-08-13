using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedColombianCitiesAndRequireStateOrProvince : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cities_Name",
                table: "Cities");

            // Backfill: ciudades de prueba existentes (E2E/QA de sesiones anteriores) tienen
            // StateOrProvince NULL o vacío. Antes de volverla NOT NULL, les asignamos un placeholder
            // descriptivo en vez de dejar que EF las deje en '' silenciosamente.
            migrationBuilder.Sql(@"
                UPDATE ""Cities"" SET ""StateOrProvince"" = 'Sin departamento'
                WHERE ""StateOrProvince"" IS NULL OR ""StateOrProvince"" = '';
            ");

            migrationBuilder.AlterColumn<string>(
                name: "StateOrProvince",
                table: "Cities",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cities_Name_StateOrProvince",
                table: "Cities",
                columns: new[] { "Name", "StateOrProvince" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cities_Name_StateOrProvince",
                table: "Cities");

            migrationBuilder.AlterColumn<string>(
                name: "StateOrProvince",
                table: "Cities",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.CreateIndex(
                name: "IX_Cities_Name",
                table: "Cities",
                column: "Name",
                unique: true);
        }
    }
}
