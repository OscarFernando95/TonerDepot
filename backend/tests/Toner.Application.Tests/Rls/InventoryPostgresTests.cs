using Microsoft.EntityFrameworkCore;
using Npgsql;
using Toner.Application.Inventory;
using Toner.Application.Inventory.Dtos;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// El saldo es una agrupación (SUM por ubicación e ítem) con joins a ítems y ubicaciones, y el listado de movimientos
// usa un cursor (fecha, id). Con InMemory solo se prueba la lógica; aquí se ejecutan contra el Postgres real.
[Collection(nameof(RlsFixtureCollection))]
public class InventoryPostgresTests
{
    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    [PostgresFact]
    public async Task Saldos_Traspasos_YMovimientos_FuncionContraPostgres()
    {
        var itemName = "PG-Item-" + Guid.NewGuid().ToString("N")[..8];
        Guid itemId, zoneLocationId = Guid.Empty, zoneId = Guid.Empty;
        await using var db = OwnerContext();
        var service = new InventoryService(db);

        try
        {
            var mainId = (await db.InventoryLocations.FirstAsync(l => l.Kind == InventoryLocationKind.Principal)).Id;
            var zone = new Zone { Name = "Zona PG inv " + Guid.NewGuid().ToString("N")[..6] };
            var location = new InventoryLocation { Kind = InventoryLocationKind.Zona, Zone = zone };
            db.AddRange(zone, location);
            await db.SaveChangesAsync();
            zoneId = zone.Id;
            zoneLocationId = location.Id;

            var item = await service.CreateItemAsync(new CreateInventoryItemRequest { Name = itemName, Category = "Repuesto", MinimumStock = 3 });
            itemId = item.Id;

            await service.RegisterEntryAsync(new RegisterEntryRequest { LocationId = mainId, ItemId = itemId, Quantity = 10 }, Guid.NewGuid());
            await service.TransferAsync(new TransferRequest { ItemId = itemId, FromLocationId = mainId, ToLocationId = zoneLocationId, Quantity = 8 }, Guid.NewGuid());

            var stock = (await service.ListStockAsync(null, itemId, null, false, null, null)).Items;
            Assert.Equal(2, stock.Single(r => r.LocationId == mainId).Quantity);
            Assert.True(stock.Single(r => r.LocationId == mainId).IsLow);   // 2 <= mínimo 3
            Assert.Equal(8, stock.Single(r => r.LocationId == zoneLocationId).Quantity);
            Assert.StartsWith("Zona PG inv", stock.Single(r => r.LocationId == zoneLocationId).LocationName);

            Assert.Single((await service.ListStockAsync(null, itemId, null, onlyLow: true, null, null)).Items);

            var first = await service.ListMovementsAsync(null, itemId, null, 2);
            Assert.Equal(2, first.Items.Count);
            var second = await service.ListMovementsAsync(null, itemId, first.NextCursor, 2);
            Assert.Single(second.Items);
            Assert.Equal(3, first.Items.Count + second.Items.Count);

            await Assert.ThrowsAsync<Toner.Application.Common.Exceptions.ConflictException>(() =>
                service.TransferAsync(new TransferRequest { ItemId = itemId, FromLocationId = mainId, ToLocationId = zoneLocationId, Quantity = 99 }, Guid.NewGuid()));
        }
        finally
        {
            await using var owner = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
            await owner.OpenAsync();
            foreach (var sql in new[]
            {
                @"DELETE FROM ""InventoryMovements"" WHERE ""InventoryItemId"" IN (SELECT ""Id"" FROM ""InventoryItems"" WHERE ""Name"" = @n)",
                @"DELETE FROM ""InventoryItems"" WHERE ""Name"" = @n",
                @"DELETE FROM ""InventoryLocations"" WHERE ""Id"" = @l",
                @"DELETE FROM ""Zones"" WHERE ""Id"" = @z"
            })
            {
                await using var cmd = owner.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("n", itemName);
                cmd.Parameters.AddWithValue("l", zoneLocationId);
                cmd.Parameters.AddWithValue("z", zoneId);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
