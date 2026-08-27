using Npgsql;

namespace Toner.Application.Tests.Rls;

// Verifica el trigger assets_sync_client_id (migración AddPhase3aDenormalizedClientId), que mantiene
// Assets."ClientId" sincronizado con la sede actual del activo.
//
// Por qué un trigger y no disciplina en C#: Assets."ClientId" es un ESTADO ACTUAL, no un hecho
// histórico — CurrentClientLocationId es mutable, y hay varios caminos que mueven un activo
// (instalación, devolución a bodega, baja). Que el aislamiento entre clientes dependa de que todos
// ellos —y el próximo que se escriba— se acuerden de actualizar la copia, es justamente lo que el
// trigger elimina. Estos tests son la contraparte: comprueban que la garantía existe de verdad.
//
// Corren como OWNER a propósito: prueban el mecanismo de sincronización, no las políticas RLS (eso
// es RowLevelSecurityTests). El owner es superusuario en el docker-compose, así que puede sembrar y
// mover activos sin toparse con las políticas.
[Collection(nameof(RlsFixtureCollection))]
public class AssetClientIdTriggerTests
{
    private readonly RlsFixture _fixture;

    public AssetClientIdTriggerTests(RlsFixture fixture) => _fixture = fixture;

    private static readonly Guid LocationOfClientA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid LocationOfClientB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private const string AssetModelId = "ffffffff-0000-0000-0000-000000000002";

    private static async Task<NpgsqlConnection> OpenOwnerAsync()
    {
        var connection = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<Guid?> ReadClientIdAsync(NpgsqlConnection connection, Guid assetId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT ""ClientId"" FROM ""Assets"" WHERE ""Id"" = @id";
        command.Parameters.AddWithValue("id", assetId);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull or null ? null : (Guid)value;
    }

    private static async Task SetLocationAsync(NpgsqlConnection connection, Guid assetId, Guid? locationId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"UPDATE ""Assets"" SET ""CurrentClientLocationId"" = @loc WHERE ""Id"" = @id";
        command.Parameters.AddWithValue("id", assetId);
        command.Parameters.AddWithValue("loc", (object?)locationId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> InsertAssetAsync(NpgsqlConnection connection, Guid? locationId)
    {
        var assetId = Guid.NewGuid();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO ""Assets"" (""Id"",""AssetModelId"",""SerialNumber"",""Type"",""LifecycleStatus"",""CurrentClientLocationId"",""CreatedAt"")
            VALUES (@id, '" + AssetModelId + @"', @sn, 'Impresora', 'Instalado', @loc, now())";
        command.Parameters.AddWithValue("id", assetId);
        command.Parameters.AddWithValue("sn", "TRIGGER-" + assetId.ToString("N")[..8]);
        command.Parameters.AddWithValue("loc", (object?)locationId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
        return assetId;
    }

    private static async Task DeleteAssetAsync(NpgsqlConnection connection, Guid assetId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"DELETE FROM ""Assets"" WHERE ""Id"" = @id";
        command.Parameters.AddWithValue("id", assetId);
        await command.ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task Insert_ConSede_DerivaElClientIdDeEsaSede()
    {
        await _fixture.EnsureSeededAsync();
        await using var db = await OpenOwnerAsync();

        var assetId = await InsertAssetAsync(db, LocationOfClientA);
        try
        {
            Assert.Equal(RlsFixture.ClientA, await ReadClientIdAsync(db, assetId));
        }
        finally
        {
            await DeleteAssetAsync(db, assetId);
        }
    }

    [PostgresFact]
    public async Task Insert_SinSede_DejaClientIdEnNull()
    {
        // Activo que nace en bodega: no pertenece a ningún cliente.
        await _fixture.EnsureSeededAsync();
        await using var db = await OpenOwnerAsync();

        var assetId = await InsertAssetAsync(db, locationId: null);
        try
        {
            Assert.Null(await ReadClientIdAsync(db, assetId));
        }
        finally
        {
            await DeleteAssetAsync(db, assetId);
        }
    }

    [PostgresFact]
    public async Task Update_AlMoverElActivoAOtroCliente_ElClientIdSigueAlPadre()
    {
        // El caso que motiva el trigger: si esto no se sincronizara, el cliente anterior seguiría
        // viendo un activo que ya no es suyo.
        await _fixture.EnsureSeededAsync();
        await using var db = await OpenOwnerAsync();

        var assetId = await InsertAssetAsync(db, LocationOfClientA);
        try
        {
            Assert.Equal(RlsFixture.ClientA, await ReadClientIdAsync(db, assetId));

            await SetLocationAsync(db, assetId, LocationOfClientB);
            Assert.Equal(RlsFixture.ClientB, await ReadClientIdAsync(db, assetId));
        }
        finally
        {
            await DeleteAssetAsync(db, assetId);
        }
    }

    [PostgresFact]
    public async Task Update_AlVolverABodega_ElClientIdVuelveANull()
    {
        await _fixture.EnsureSeededAsync();
        await using var db = await OpenOwnerAsync();

        var assetId = await InsertAssetAsync(db, LocationOfClientA);
        try
        {
            Assert.Equal(RlsFixture.ClientA, await ReadClientIdAsync(db, assetId));

            await SetLocationAsync(db, assetId, locationId: null);
            Assert.Null(await ReadClientIdAsync(db, assetId));
        }
        finally
        {
            await DeleteAssetAsync(db, assetId);
        }
    }

    [PostgresFact]
    public async Task Update_QueIntentaEscribirClientIdDirecto_EsRecalculadoDesdeElOrigen()
    {
        // El trigger se dispara en TODO UPDATE, no solo cuando cambia CurrentClientLocationId: así un
        // UPDATE que tocara "ClientId" a mano (o por un bug) no puede dejar la copia divergente.
        await _fixture.EnsureSeededAsync();
        await using var db = await OpenOwnerAsync();

        var assetId = await InsertAssetAsync(db, LocationOfClientA);
        try
        {
            await using (var tamper = db.CreateCommand())
            {
                tamper.CommandText = @"UPDATE ""Assets"" SET ""ClientId"" = @otro WHERE ""Id"" = @id";
                tamper.Parameters.AddWithValue("otro", RlsFixture.ClientB);
                tamper.Parameters.AddWithValue("id", assetId);
                await tamper.ExecuteNonQueryAsync();
            }

            // Sigue siendo el cliente A: el trigger lo recalculó desde CurrentClientLocationId.
            Assert.Equal(RlsFixture.ClientA, await ReadClientIdAsync(db, assetId));
        }
        finally
        {
            await DeleteAssetAsync(db, assetId);
        }
    }

    [PostgresFact]
    public async Task NingunActivoQuedaDesincronizadoConSuSede()
    {
        // Test de deriva: complementa al trigger en vez de duplicarlo. Si alguien agrega un camino de
        // escritura nuevo que de algún modo evite la sincronización, esto lo detecta.
        await _fixture.EnsureSeededAsync();
        await using var db = await OpenOwnerAsync();

        await using var command = db.CreateCommand();
        command.CommandText = @"
            SELECT count(*) FROM ""Assets"" a
            LEFT JOIN ""ClientLocations"" cl ON cl.""Id"" = a.""CurrentClientLocationId""
            WHERE a.""ClientId"" IS DISTINCT FROM cl.""ClientId""";

        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }
}
