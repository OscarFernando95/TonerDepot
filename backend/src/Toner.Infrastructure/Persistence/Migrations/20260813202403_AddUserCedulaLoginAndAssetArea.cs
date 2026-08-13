using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCedulaLoginAndAssetArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Users",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cedula",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CityId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "Assets",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            // Backfill: la columna Cedula recién agregada queda '' en todas las filas existentes
            // (defaultValue de una columna NOT NULL). Antes de crear el índice único hay que asignar un
            // valor único de verdad a cada usuario existente, o la creación del índice fallaría (todas
            // las filas colisionarían en ''). El offset 9000000000 + ROW_NUMBER() garantiza unicidad por
            // construcción, sin depender de random() contra una constraint real, y no puede chocar con
            // el valor fijo del admin de arranque.
            migrationBuilder.Sql(@"
                UPDATE ""Users"" SET ""Cedula"" = sub.""SyntheticCedula""
                FROM (
                    SELECT ""Id"", (9000000000 + ROW_NUMBER() OVER (ORDER BY ""CreatedAt""))::text AS ""SyntheticCedula""
                    FROM ""Users"" WHERE ""Cedula"" = ''
                ) AS sub
                WHERE ""Users"".""Id"" = sub.""Id"";

                UPDATE ""Users"" SET ""Cedula"" = '1234567890' WHERE ""Email"" = 'admin@toner.local';
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Cedula",
                table: "Users",
                column: "Cedula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CityId",
                table: "Users",
                column: "CityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Cities_CityId",
                table: "Users",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Cities_CityId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Cedula",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_CityId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Cedula",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Area",
                table: "Assets");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }
    }
}
