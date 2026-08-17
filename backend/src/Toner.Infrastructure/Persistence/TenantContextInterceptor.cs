using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Toner.Application.Common.Interfaces;

namespace Toner.Infrastructure.Persistence;

// Propaga el contexto de tenencia a Postgres como variables de sesión, para que las políticas RLS
// (ver migración AddRowLevelSecurity) puedan filtrar por cliente — SECURITY_AUDIT.md hallazgo #5.
//
// Por qué SET de sesión y no SET LOCAL: SET LOCAL solo dura hasta el fin de la transacción, y EF
// Core corre las consultas en autocommit (sin transacción explícita) — ahí un SET LOCAL no hace
// nada y solo emite un WARNING silencioso. El SET de sesión es seguro con Npgsql porque el pool
// resetea el estado de la sesión al devolver la conexión (ver NpgsqlSessionResetTests, que verifica
// esa garantía contra un Postgres real: todo este diseño depende de ella).
//
// Por qué set_config() y no "SET app.x = '...'": SET no acepta parámetros vinculados, así que
// construirlo requeriría concatenar la cadena — reintroduciendo un vector de inyección en el único
// proyecto que hoy no tiene una sola línea de SQL crudo sin parametrizar (hallazgo #2 de la
// auditoría, sección SQL Injection). set_config() sí acepta parámetros.
public sealed class TenantContextInterceptor : DbConnectionInterceptor
{
    private const string ApplySql =
        "SELECT set_config('app.is_staff', @is_staff, false), " +
        "       set_config('app.current_client_id', @current_client_id, false)";

    private readonly ITenantContextAccessor _tenantContextAccessor;

    public TenantContextInterceptor(ITenantContextAccessor tenantContextAccessor)
    {
        _tenantContextAccessor = tenantContextAccessor;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        // Falla ruidosamente en vez de dejar la sesión sin setear: si las variables quedaran sin
        // valor, las políticas RLS devolverían 0 filas en los SELECT — un resultado vacío es
        // indistinguible de "este cliente no tiene datos", así que el bug pasaría inadvertido.
        // Un código que llega aquí sin contexto es un camino que no pasó por TenantContextMiddleware
        // ni estableció un scope explícito (ver DataSeeder / los jobs de Hangfire).
        var context = _tenantContextAccessor.Current
            ?? throw new InvalidOperationException(
                "No hay TenantContext establecido para esta conexión. Todo acceso a la base de datos " +
                "debe correr dentro de una request HTTP (TenantContextMiddleware) o de un scope " +
                "explícito (ITenantContextAccessor.Push, ej. TenantContext.Staff para trabajo en " +
                "background). Ver SECURITY_AUDIT.md hallazgo #5.");

        var command = connection.CreateCommand();
        command.CommandText = ApplySql;

        var isStaff = command.CreateParameter();
        isStaff.ParameterName = "is_staff";
        isStaff.Value = context.IsStaff ? "on" : "off";
        command.Parameters.Add(isStaff);

        var clientId = command.CreateParameter();
        clientId.ParameterName = "current_client_id";
        // Cadena vacía (no NULL) cuando no hay cliente: '"ClientId"::text = ''' es false, así que
        // las políticas deniegan. NULL haría que la comparación fuera NULL, que también deniega,
        // pero la cadena vacía deja el estado explícito y legible al depurar con current_setting().
        clientId.Value = context.ClientId?.ToString() ?? string.Empty;
        command.Parameters.Add(clientId);

        return command;
    }
}
