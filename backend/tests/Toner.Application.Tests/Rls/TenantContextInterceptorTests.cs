using Npgsql;
using Toner.Application.Common;
using Toner.Application.Common.Interfaces;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

public class TenantContextInterceptorTests
{
    [Fact]
    public async Task SinContextoEstablecido_Lanza_EnVezDeDejarLaSesionSinSetear()
    {
        // Riesgo 3 del diseño: si las variables quedaran sin setear, las políticas RLS devolverían 0
        // filas en los SELECT — indistinguible de "este cliente no tiene datos". Un fallo silencioso
        // de datos es peor que un 500, así que el interceptor debe lanzar.
        var interceptor = new TenantContextInterceptor(new TenantContextAccessor());
        await using var connection = new NpgsqlConnection(PostgresFactAttribute.ConnectionString);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            interceptor.ConnectionOpenedAsync(connection, eventData: null!, CancellationToken.None));

        Assert.Contains("TenantContext", ex.Message);
    }

    [Fact]
    public void Accessor_RestauraElContextoAnteriorAlSalirDelScope()
    {
        var accessor = new TenantContextAccessor();
        Assert.Null(accessor.Current);

        using (accessor.Push(() => TenantContext.Staff))
        {
            Assert.True(accessor.Current!.IsStaff);

            var clientId = Guid.NewGuid();
            using (accessor.Push(() => TenantContext.ForClient(clientId)))
            {
                Assert.False(accessor.Current!.IsStaff);
                Assert.Equal(clientId, accessor.Current.ClientId);
            }

            // Al cerrar el scope anidado vuelve el de staff, no queda "pegado" el del cliente.
            Assert.True(accessor.Current!.IsStaff);
        }

        Assert.Null(accessor.Current);
    }

    [PostgresFact]
    public async Task Npgsql_ReseteaElEstadoDeSesionAlDevolverLaConexionAlPool()
    {
        // Esta es LA garantía de la que depende todo el diseño: el interceptor usa SET de sesión (no
        // SET LOCAL, que no funciona fuera de una transacción y EF corre en autocommit). Si Npgsql no
        // limpiara el estado al devolver la conexión al pool, el contexto de un usuario se filtraría a
        // la siguiente request que reutilice esa conexión física — un cross-tenant leak.
        //
        // "Maximum Pool Size=1" fuerza a que la segunda apertura reutilice la MISMA conexión física,
        // que es justo el escenario peligroso.
        var connectionString = PostgresFactAttribute.ConnectionString + ";Maximum Pool Size=1";

        int firstProcessId;
        await using (var first = new NpgsqlConnection(connectionString))
        {
            await first.OpenAsync();
            firstProcessId = first.ProcessID;

            await using var set = first.CreateCommand();
            set.CommandText = "SELECT set_config('app.current_client_id', @v, false)";
            set.Parameters.AddWithValue("v", Guid.NewGuid().ToString());
            await set.ExecuteNonQueryAsync();
        }

        await using var second = new NpgsqlConnection(connectionString);
        await second.OpenAsync();

        Assert.Equal(firstProcessId, second.ProcessID); // misma conexión física reutilizada

        await using var read = second.CreateCommand();
        read.CommandText = "SELECT current_setting('app.current_client_id', true)";
        var leaked = await read.ExecuteScalarAsync() as string;

        Assert.True(
            string.IsNullOrEmpty(leaked),
            $"El estado de sesión sobrevivió al pool ('{leaked}'). Todo el diseño de RLS depende de " +
            "que Npgsql lo resetee: si esto falla, el contexto de tenencia se filtra entre requests.");
    }
}
