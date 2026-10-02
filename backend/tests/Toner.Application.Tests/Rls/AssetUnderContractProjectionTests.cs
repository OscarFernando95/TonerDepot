using Microsoft.EntityFrameworkCore;
using Npgsql;
using Toner.Application.Common;
using Toner.Application.Tickets;
using Toner.Domain.Common;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// ServiceTicketDto.AssetUnderContract decide si la foto del contador es obligatoria al cerrar un ticket. Es un
// EXISTS correlacionado sobre ContractAssets + Contracts: con InMemory solo se prueba la lógica, no que traduzca
// ni que RLS (ambas tablas tienen política) deje ver el vínculo. Aquí corre contra el Postgres real, con los dos
// roles de la app.
[Collection(nameof(RlsFixtureCollection))]
public class AssetUnderContractProjectionTests
{
    private readonly RlsFixture _fixture;

    public AssetUnderContractProjectionTests(RlsFixture fixture) => _fixture = fixture;

    private static readonly Guid LocationA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid ContractA = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private const string ModelId = "ffffffff-0000-0000-0000-000000000002";

    private sealed record Seeded(Guid TicketVigente, Guid TicketSinContrato, Guid TicketContratoVencido, Guid TicketSinActivo, Guid ExpiredContract, Guid[] Assets);

    private static async Task ExecAsync(NpgsqlConnection c, string sql, params (string, object)[] args)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<Seeded> SeedAsync(NpgsqlConnection owner)
    {
        var assetOk = Guid.NewGuid();
        var assetFree = Guid.NewGuid();
        var assetExpired = Guid.NewGuid();
        var expiredContract = Guid.NewGuid();
        var tOk = Guid.NewGuid();
        var tFree = Guid.NewGuid();
        var tExpired = Guid.NewGuid();
        var tNoAsset = Guid.NewGuid();

        foreach (var (id, sn) in new[] { (assetOk, "UC-OK"), (assetFree, "UC-FREE"), (assetExpired, "UC-EXP") })
        {
            await ExecAsync(owner,
                @"INSERT INTO ""Assets"" (""Id"",""AssetModelId"",""SerialNumber"",""Type"",""LifecycleStatus"",""CurrentClientLocationId"",""CreatedAt"")
                  VALUES (@id,'" + ModelId + @"',@sn,'Impresora','Instalado',@loc,now())",
                ("id", id), ("sn", $"{sn}-{id:N}"[..16]), ("loc", LocationA));
        }

        await ExecAsync(owner,
            @"INSERT INTO ""Contracts"" (""Id"",""ClientId"",""StartDate"",""Status"",""CreatedAt"") VALUES (@id,@c,now(),'Vencido',now())",
            ("id", expiredContract), ("c", RlsFixture.ClientA));

        await ExecAsync(owner,
            @"INSERT INTO ""ContractAssets"" (""Id"",""ContractId"",""ClientId"",""AssetId"",""StartDate"",""CreatedAt"") VALUES (@id,@k,@c,@a,now(),now())",
            ("id", Guid.NewGuid()), ("k", ContractA), ("c", RlsFixture.ClientA), ("a", assetOk));
        await ExecAsync(owner,
            @"INSERT INTO ""ContractAssets"" (""Id"",""ContractId"",""ClientId"",""AssetId"",""StartDate"",""CreatedAt"") VALUES (@id,@k,@c,@a,now(),now())",
            ("id", Guid.NewGuid()), ("k", expiredContract), ("c", RlsFixture.ClientA), ("a", assetExpired));

        foreach (var (id, asset) in new (Guid, Guid?)[] { (tOk, assetOk), (tFree, assetFree), (tExpired, assetExpired), (tNoAsset, null) })
        {
            await ExecAsync(owner,
                @"INSERT INTO ""ServiceTickets"" (""Id"",""ClientLocationId"",""ClientId"",""AssetId"",""ReportedByUserId"",""Description"",""Status"",""Priority"",""CreatedAt"")
                  SELECT @id,@loc,@c,@asset,u.""Id"",'UC test','Abierto','Media',now() FROM ""Users"" u LIMIT 1",
                ("id", id), ("loc", LocationA), ("c", RlsFixture.ClientA), ("asset", (object?)asset ?? DBNull.Value));
        }

        return new Seeded(tOk, tFree, tExpired, tNoAsset, expiredContract, new[] { assetOk, assetFree, assetExpired });
    }

    private static async Task CleanupAsync(NpgsqlConnection owner, Seeded s)
    {
        await ExecAsync(owner, @"DELETE FROM ""ServiceTickets"" WHERE ""Id"" = ANY(@ids)",
            ("ids", new[] { s.TicketVigente, s.TicketSinContrato, s.TicketContratoVencido, s.TicketSinActivo }));
        await ExecAsync(owner, @"DELETE FROM ""ContractAssets"" WHERE ""AssetId"" = ANY(@ids)", ("ids", s.Assets));
        await ExecAsync(owner, @"DELETE FROM ""Contracts"" WHERE ""Id"" = @id", ("id", s.ExpiredContract));
        await ExecAsync(owner, @"DELETE FROM ""Assets"" WHERE ""Id"" = ANY(@ids)", ("ids", s.Assets));
    }

    // Misma sesión que arma TenantContextInterceptor en producción: staff = SET ROLE toner_app_staff; cliente = rol
    // de la app + app.current_client_id.
    private static async Task<(TonerDbContext Db, NpgsqlConnection Conn)> OpenContextAsync(bool isStaff, Guid? clientId)
    {
        var conn = new NpgsqlConnection(PostgresFactAttribute.ConnectionString);
        await conn.OpenAsync();
        await ExecAsync(conn, "SELECT set_config('role', @role, false), set_config('app.current_client_id', @c, false)",
            ("role", isStaff ? "toner_app_staff" : "none"), ("c", clientId?.ToString() ?? string.Empty));
        var options = new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(conn).Options;
        return (new TonerDbContext(options), conn);
    }

    private static async Task AssertFlagsAsync(bool isStaff, Guid? clientId, Seeded s)
    {
        var (db, conn) = await OpenContextAsync(isStaff, clientId);
        await using var _ = conn;
        using var __ = db;
        var user = new RequestingUser(Guid.NewGuid(), isStaff ? RoleNames.Administrador : RoleNames.Cliente, clientId, null);
        var service = new ServiceTicketService(db, null!);

        Assert.True((await service.GetByIdAsync(user, s.TicketVigente)).AssetUnderContract);
        Assert.False((await service.GetByIdAsync(user, s.TicketSinContrato)).AssetUnderContract);
        Assert.False((await service.GetByIdAsync(user, s.TicketContratoVencido)).AssetUnderContract);
        Assert.False((await service.GetByIdAsync(user, s.TicketSinActivo)).AssetUnderContract);
    }

    [PostgresFact]
    public async Task Staff_VeAssetUnderContract_SoloConContratoActivoVigente()
    {
        await _fixture.EnsureSeededAsync();
        await using var owner = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
        await owner.OpenAsync();
        var seeded = await SeedAsync(owner);
        try
        {
            await AssertFlagsAsync(isStaff: true, clientId: null, seeded);
        }
        finally
        {
            await CleanupAsync(owner, seeded);
        }
    }

    [PostgresFact]
    public async Task Cliente_ConRls_VeElMismoResultadoParaSusPropiosTickets()
    {
        await _fixture.EnsureSeededAsync();
        await using var owner = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
        await owner.OpenAsync();
        var seeded = await SeedAsync(owner);
        try
        {
            await AssertFlagsAsync(isStaff: false, clientId: RlsFixture.ClientA, seeded);
        }
        finally
        {
            await CleanupAsync(owner, seeded);
        }
    }
}
