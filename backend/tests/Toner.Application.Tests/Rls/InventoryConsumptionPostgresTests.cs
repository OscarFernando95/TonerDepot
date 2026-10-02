using Microsoft.EntityFrameworkCore;
using Npgsql;
using Toner.Application.Common;
using Toner.Application.Inventory;
using Toner.Application.Inventory.Dtos;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// InventoryMovements lleva ClientId (el consumo cuelga de un cliente) y su política RLS; las consultas del kit, de la
// búsqueda de repuestos y del historial de tóner corren contra el Postgres real.
[Collection(nameof(RlsFixtureCollection))]
public class InventoryConsumptionPostgresTests
{
    private readonly RlsFixture _fixture;

    public InventoryConsumptionPostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    private static async Task ExecAsync(NpgsqlConnection c, string sql, params (string, object)[] args)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountForAsync(bool isStaff, Guid? clientId, Guid itemId)
    {
        await using var c = new NpgsqlConnection(PostgresFactAttribute.ConnectionString);
        await c.OpenAsync();
        await ExecAsync(c, "SELECT set_config('role', @role, false), set_config('app.current_client_id', @c, false)",
            ("role", isStaff ? "toner_app_staff" : "none"), ("c", clientId?.ToString() ?? string.Empty));
        await using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT count(*) FROM ""InventoryMovements"" WHERE ""InventoryItemId"" = @i";
        cmd.Parameters.AddWithValue("i", itemId);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task CleanupAsync(string itemName)
    {
        await using var owner = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
        await owner.OpenAsync();
        await ExecAsync(owner, @"DELETE FROM ""InventoryMovements"" WHERE ""InventoryItemId"" IN (SELECT ""Id"" FROM ""InventoryItems"" WHERE ""Name"" = @n)", ("n", itemName));
        await ExecAsync(owner, @"DELETE FROM ""InventoryItems"" WHERE ""Name"" = @n", ("n", itemName));
    }

    [PostgresFact]
    public async Task RLS_UnClienteSoloVeElConsumoDeSusMaquinas_NuncaLosMovimientosDeLaEmpresa()
    {
        await _fixture.EnsureSeededAsync();
        var itemName = "PG-RLS-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            await using var db = OwnerContext();
            var mainId = (await db.InventoryLocations.FirstAsync(l => l.Kind == InventoryLocationKind.Principal)).Id;
            var item = new InventoryItem { Name = itemName, Category = InventoryCategory.Repuesto };
            db.Add(item);
            db.InventoryMovements.AddRange(
                new InventoryMovement { InventoryItemId = item.Id, InventoryLocationId = mainId, Type = InventoryMovementType.Entrada, Delta = 10 },
                new InventoryMovement { InventoryItemId = item.Id, InventoryLocationId = mainId, Type = InventoryMovementType.Consumo, Delta = -1, ClientId = RlsFixture.ClientA, AssetId = RlsFixture.AssetOfClientA },
                new InventoryMovement { InventoryItemId = item.Id, InventoryLocationId = mainId, Type = InventoryMovementType.Consumo, Delta = -2, ClientId = RlsFixture.ClientB, AssetId = RlsFixture.AssetOfClientB });
            await db.SaveChangesAsync();

            Assert.Equal(1, await CountForAsync(false, RlsFixture.ClientA, item.Id));
            Assert.Equal(1, await CountForAsync(false, RlsFixture.ClientB, item.Id));
            Assert.Equal(0, await CountForAsync(false, Guid.NewGuid(), item.Id));
            Assert.Equal(3, await CountForAsync(true, null, item.Id));

            // Un cliente no puede escribir consumo a nombre de otro, ni movimientos de la empresa (ClientId NULL).
            await using var c = new NpgsqlConnection(PostgresFactAttribute.ConnectionString);
            await c.OpenAsync();
            await ExecAsync(c, "SELECT set_config('role', 'none', false), set_config('app.current_client_id', @c, false)", ("c", RlsFixture.ClientA.ToString()));
            const string insert = @"INSERT INTO ""InventoryMovements"" (""Id"",""InventoryItemId"",""InventoryLocationId"",""Type"",""Delta"",""ClientId"",""OccurredAt"",""CreatedAt"")
                                    VALUES (@id,@i,@l,'Consumo',-1,@client,now(),now())";
            var cross = await Assert.ThrowsAsync<PostgresException>(() => ExecAsync(c, insert,
                ("id", Guid.NewGuid()), ("i", item.Id), ("l", mainId), ("client", RlsFixture.ClientB)));
            Assert.Equal("42501", cross.SqlState);
            var company = await Assert.ThrowsAsync<PostgresException>(() => ExecAsync(c, insert,
                ("id", Guid.NewGuid()), ("i", item.Id), ("l", mainId), ("client", DBNull.Value)));
            Assert.Equal("42501", company.SqlState);
        }
        finally
        {
            await CleanupAsync(itemName);
        }
    }

    [PostgresFact]
    public async Task KitRepuestosYToner_FuncionContraPostgres()
    {
        await _fixture.EnsureSeededAsync();
        var tonerName = "PG-Toner-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            await using var db = OwnerContext();
            var service = new InventoryConsumptionService(db, new BaseKitService(db));
            var staff = new RequestingUser(Guid.NewGuid(), RoleNames.Administrador, null, null);
            var toner = new InventoryItem { Name = tonerName, Category = InventoryCategory.Toner };
            db.Add(toner);
            await db.SaveChangesAsync();

            // El municipio del fixture no tiene zona: cae a la bodega principal.
            var kit = await service.GetKitForAssetAsync(RlsFixture.AssetOfClientA);
            Assert.True(kit.UsesMainWarehouse);

            var parts = await service.SearchPartsAsync(RlsFixture.AssetOfClientA, tonerName[..12], null, null);
            Assert.Equal(tonerName, Assert.Single(parts.Items).Name);

            var entry = await service.RegisterTonerAsync(
                new RegisterTonerRequest { AssetId = RlsFixture.AssetOfClientA, ItemId = toner.Id, Quantity = 2, OccurredAt = DateTime.UtcNow.AddDays(-3), CounterValue = 500 }, staff);
            Assert.Equal(2, entry.Quantity);
            Assert.NotNull(entry.StockWarning);   // sin stock: avisa, no bloquea

            var history = await service.ListTonerAsync(RlsFixture.AssetOfClientA, DateTime.UtcNow.AddDays(-10), null, null, null, staff);
            Assert.Equal(tonerName, Assert.Single(history.Items.Where(h => h.ItemName == tonerName)).ItemName);
            Assert.Empty((await service.ListTonerAsync(RlsFixture.AssetOfClientA, DateTime.UtcNow.AddDays(-1), null, null, null, staff)).Items.Where(h => h.ItemName == tonerName));
        }
        finally
        {
            await CleanupAsync(tonerName);
        }
    }
}
