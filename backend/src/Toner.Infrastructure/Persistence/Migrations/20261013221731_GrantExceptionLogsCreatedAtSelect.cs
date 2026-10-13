using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantExceptionLogsCreatedAtSelect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AUDIT.md H-27/H-38. AddRowLevelSecurity le quitó a toner_app todo SELECT sobre ExceptionLogs
            // (stack traces y correos: hallazgo #14). La purga por antigüedad necesita leer SOLO la fecha
            // en el WHERE, así que se concede SELECT sobre la columna "CreatedAt" y nada más: con ese
            // privilegio la app no puede leer ni el stack, ni el mensaje, ni el correo (verificado en
            // DataRetentionPostgresTests). DELETE ya lo tenía.
            //
            // También a toner_app_staff: los jobs corren con TenantContext.Staff, que hace SET ROLE
            // toner_app_staff, y bajo ese rol solo cuentan los privilegios de ESE rol (toner_app es
            // NOINHERIT). Sin esto el job sigue dando 42501.
            migrationBuilder.Sql(@"GRANT SELECT (""CreatedAt"") ON ""ExceptionLogs"" TO toner_app, toner_app_staff;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"REVOKE SELECT (""CreatedAt"") ON ""ExceptionLogs"" FROM toner_app, toner_app_staff;");
        }
    }
}
