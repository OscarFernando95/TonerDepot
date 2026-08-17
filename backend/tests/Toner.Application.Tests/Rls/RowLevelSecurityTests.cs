using Npgsql;

namespace Toner.Application.Tests.Rls;

// Verifica el hallazgo #5 de SECURITY_AUDIT.md contra el Postgres real: que RLS sea una capa de
// verdad y no decorativa. Estos tests van deliberadamente POR DEBAJO de la capa de aplicación —
// hablan SQL directo como el rol toner_app, ignorando por completo los filtros de C#. Si RLS no
// estuviera aplicando, verían datos de otros clientes.
//
// El filtrado en C# (RequestingUser + los Where de cada servicio) sigue intacto y sigue siendo la
// primera línea de defensa; RLS es la segunda.
[Collection(nameof(RlsFixtureCollection))]
public class RowLevelSecurityTests
{
    private readonly RlsFixture _fixture;

    public RowLevelSecurityTests(RlsFixture fixture) => _fixture = fixture;

    private static async Task<NpgsqlConnection> OpenAsAsync(bool isStaff, Guid? clientId)
    {
        var connection = new NpgsqlConnection(PostgresFactAttribute.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT set_config('app.is_staff', @is_staff, false), " +
            "       set_config('app.current_client_id', @client_id, false)";
        command.Parameters.AddWithValue("is_staff", isStaff ? "on" : "off");
        command.Parameters.AddWithValue("client_id", clientId?.ToString() ?? string.Empty);
        await command.ExecuteNonQueryAsync();

        return connection;
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $@"SELECT count(*) FROM ""{table}""";
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [PostgresFact]
    public async Task Cliente_SoloVeSusPropiasFilas_AunConSqlDirectoQueIgnoraElFiltroDeCSharp()
    {
        await _fixture.EnsureSeededAsync();

        await using var asClientA = await OpenAsAsync(isStaff: false, RlsFixture.ClientA);
        Assert.Equal(1, await CountAsync(asClientA, "ClientLocations"));
        Assert.Equal(1, await CountAsync(asClientA, "Contracts"));
        Assert.Equal(1, await CountAsync(asClientA, "ServiceTickets"));

        await using var asClientB = await OpenAsAsync(isStaff: false, RlsFixture.ClientB);
        Assert.Equal(1, await CountAsync(asClientB, "ClientLocations"));
        Assert.Equal(1, await CountAsync(asClientB, "Contracts"));
        // El único ticket pertenece al cliente A: B no debe verlo ni con SQL crudo.
        Assert.Equal(0, await CountAsync(asClientB, "ServiceTickets"));
    }

    [PostgresFact]
    public async Task Cliente_NoPuedeInsertarFilaAtribuidaAOtroCliente()
    {
        await _fixture.EnsureSeededAsync();

        await using var asClientB = await OpenAsAsync(isStaff: false, RlsFixture.ClientB);
        await using var command = asClientB.CreateCommand();
        command.CommandText = @"
            INSERT INTO ""ClientLocations"" (""Id"",""ClientId"",""CityId"",""Name"",""Address"",""IsActive"",""CreatedAt"")
            VALUES (gen_random_uuid(), @otherClient, @cityId, 'Sede pirata', 'X', true, now())";
        command.Parameters.AddWithValue("otherClient", RlsFixture.ClientA);
        command.Parameters.AddWithValue("cityId", RlsFixture.CityId);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("42501", ex.SqlState); // insufficient_privilege / RLS violation
        Assert.Contains("row-level security", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task Cliente_NoPuedeRobarFilaDeOtroClienteConUpdate()
    {
        await _fixture.EnsureSeededAsync();

        await using var asClientB = await OpenAsAsync(isStaff: false, RlsFixture.ClientB);
        await using var command = asClientB.CreateCommand();
        // Intento de apropiarse del contrato del cliente A reasignándolo a sí mismo.
        command.CommandText = @"UPDATE ""Contracts"" SET ""ClientId"" = @me WHERE ""ClientId"" = @other";
        command.Parameters.AddWithValue("me", RlsFixture.ClientB);
        command.Parameters.AddWithValue("other", RlsFixture.ClientA);

        // La política USING ni siquiera deja ver la fila, así que el UPDATE afecta 0 filas.
        Assert.Equal(0, await command.ExecuteNonQueryAsync());
    }

    [PostgresFact]
    public async Task Staff_VeTodosLosClientes()
    {
        await _fixture.EnsureSeededAsync();

        await using var asStaff = await OpenAsAsync(isStaff: true, clientId: null);
        Assert.True(await CountAsync(asStaff, "ClientLocations") >= 2);
        Assert.True(await CountAsync(asStaff, "Contracts") >= 2);
        Assert.True(await CountAsync(asStaff, "ServiceTickets") >= 1);
    }

    [PostgresFact]
    public async Task SinContextoDeSesion_NoSeVeNingunaFila()
    {
        await _fixture.EnsureSeededAsync();

        // Simula el escenario que el TenantContextInterceptor previene lanzando: una conexión donde
        // nadie seteó las variables. RLS deniega todo — fail-closed. (En la app esto nunca llega a
        // pasar porque el interceptor lanza antes; ver TenantContextInterceptorTests.)
        await using var connection = new NpgsqlConnection(PostgresFactAttribute.ConnectionString);
        await connection.OpenAsync();

        Assert.Equal(0, await CountAsync(connection, "ClientLocations"));
        Assert.Equal(0, await CountAsync(connection, "Contracts"));
        Assert.Equal(0, await CountAsync(connection, "ServiceTickets"));
    }

    [PostgresFact]
    public async Task ToneApp_NoPuedeDesactivarRlsNiHacerDdlSobreTablasDeNegocio()
    {
        // La propiedad de seguridad que justifica todo el fix: aunque un atacante logre ejecutar SQL
        // arbitrario como la aplicación, no puede apagar las políticas ni destruir el esquema.
        await using var connection = new NpgsqlConnection(PostgresFactAttribute.ConnectionString);
        await connection.OpenAsync();

        await using (var disableRls = connection.CreateCommand())
        {
            disableRls.CommandText = @"ALTER TABLE ""Contracts"" DISABLE ROW LEVEL SECURITY";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => disableRls.ExecuteNonQueryAsync());
            Assert.Contains("must be owner", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using (var drop = connection.CreateCommand())
        {
            drop.CommandText = @"DROP TABLE ""Clients""";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => drop.ExecuteNonQueryAsync());
            Assert.Contains("must be owner", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using (var readLogs = connection.CreateCommand())
        {
            // ExceptionLogs guarda stack traces y correos: la app escribe pero no debe poder leer.
            readLogs.CommandText = @"SELECT count(*) FROM ""ExceptionLogs""";
            var ex = await Assert.ThrowsAsync<PostgresException>(() => readLogs.ExecuteScalarAsync());
            Assert.Equal("42501", ex.SqlState);
        }
    }
}
