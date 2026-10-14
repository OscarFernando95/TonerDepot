using Npgsql;

namespace Toner.Infrastructure.Persistence;

// La app NO puede conectarse a través de un pooler en modo transacción (AUDIT.md H-43).
//
// TenantContextInterceptor fija el contexto RLS con set_config('role', …, false) y
// set_config('app.current_client_id', …, false): ámbito de SESIÓN, una vez por conexión física. Eso exige que una
// conexión cliente sea una sesión de servidor. El PgBouncer integrado de Azure Database for PostgreSQL Flexible
// Server escucha en 6432 en modo TRANSACCIÓN y reparte las conexiones de servidor entre clientes distintos.
// Verificado localmente (PgBouncer 1.25.1, pool_mode=transaction): un cliente nuevo heredó el rol toner_app_staff y
// el client_id que había fijado otro → un usuario cualquiera vería los datos de todos los clientes.
//
// Comprobación por puerto, a propósito: es determinista (en Azure el pooler integrado solo está en 6432). Una sonda en
// runtime no lo es: con poca carga PgBouncer puede devolver la misma conexión de servidor y la sonda pasaría en falso.
public static class PostgresConnectionGuard
{
    public const int AzureTransactionPoolerPort = 6432;

    public static void EnsureNoTransactionPooler(string? connectionString, string name)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return; // la falta de cadena la reporta su propio validador/uso
        }

        var port = new NpgsqlConnectionStringBuilder(connectionString).Port;
        if (port == AzureTransactionPoolerPort)
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{name} apunta al puerto {AzureTransactionPoolerPort}, el PgBouncer de Azure en modo transacción. " +
                "La aplicación fija el contexto de RLS por sesión (SET ROLE / set_config) y ese pooler lo comparte entre " +
                "usuarios distintos: un usuario podría ver los datos de otro cliente. Conéctate directo al puerto 5432. " +
                "Si algún día hace falta un pooler externo, debe ser en modo sesión y requiere revisar TenantContextInterceptor.");
        }
    }
}
