using Microsoft.EntityFrameworkCore;
using Npgsql;
using Toner.Application.Inventory.Analytics;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// El BI filtra con una subconsulta sobre activos (zona, cliente, marca, modelo) y proyecta varios joins: con InMemory
// solo se prueba la lógica; aquí corre contra el Postgres real.
[Collection(nameof(RlsFixtureCollection))]
public class TonerAnalyticsPostgresTests
{
    private readonly RlsFixture _fixture;

    public TonerAnalyticsPostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static readonly Guid FixtureBrand = Guid.Parse("ffffffff-0000-0000-0000-000000000001");
    private static readonly Guid FixtureModel = Guid.Parse("ffffffff-0000-0000-0000-000000000002");

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    [PostgresFact]
    public async Task Resumen_Maquinas_YCsv_FuncionContraPostgres()
    {
        await _fixture.EnsureSeededAsync();
        var itemName = "PG-BI-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            await using var db = OwnerContext();
            var mainId = (await db.InventoryLocations.FirstAsync(l => l.Kind == InventoryLocationKind.Principal)).Id;
            var toner = new InventoryItem { Name = itemName, Category = InventoryCategory.Toner };
            db.Add(toner);
            var now = DateTime.UtcNow;
            InventoryMovement Move(Guid asset, Guid client, DateTime at, int qty, long counter, bool delivered) => new()
            {
                InventoryItemId = toner.Id, InventoryLocationId = mainId, Type = InventoryMovementType.Consumo, Delta = -qty,
                AssetId = asset, ClientId = client, OccurredAt = at, CounterValue = counter, DeliveredToUser = delivered
            };
            db.InventoryMovements.AddRange(
                Move(RlsFixture.AssetOfClientA, RlsFixture.ClientA, now.AddDays(-40), 1, 1000, false),
                Move(RlsFixture.AssetOfClientA, RlsFixture.ClientA, now.AddDays(-10), 1, 5000, true),
                Move(RlsFixture.AssetOfClientB, RlsFixture.ClientB, now.AddDays(-30), 2, 0, false));
            await db.SaveChangesAsync();

            var service = new TonerAnalyticsService(db, TimeProvider.System);
            var byModel = new TonerFilter { ModelId = FixtureModel };

            var summary = await service.GetSummaryAsync(byModel);
            Assert.Equal(4, summary.TotalUnits);
            Assert.Equal(2, summary.Machines);
            Assert.Equal(1, summary.DeliveredToUserUnits);
            Assert.Equal(4000, summary.AvgPagesPerUnit);   // A: 4000 páginas con 1 tóner

            var onlyA = await service.ListMachinesAsync(new TonerFilter { ModelId = FixtureModel, ClientId = RlsFixture.ClientA }, null, null);
            Assert.Equal("Sede A", Assert.Single(onlyA.Items).LocationName);

            Assert.Equal(2, (await service.ListMachinesAsync(new TonerFilter { BrandId = FixtureBrand }, null, null)).TotalCount);
            Assert.Empty((await service.ListMachinesAsync(new TonerFilter { ModelId = FixtureModel, ZoneId = Guid.NewGuid() }, null, null)).Items);
            Assert.Empty((await service.ListMachinesAsync(new TonerFilter { ModelId = FixtureModel, From = now.AddDays(-5) }, null, null)).Items);

            var csv = await service.ExportCsvAsync(byModel);
            Assert.StartsWith("﻿Marca;Modelo", csv);
            Assert.Contains("Sede A", csv);
        }
        finally
        {
            await using var owner = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
            await owner.OpenAsync();
            foreach (var sql in new[]
            {
                @"DELETE FROM ""InventoryMovements"" WHERE ""InventoryItemId"" IN (SELECT ""Id"" FROM ""InventoryItems"" WHERE ""Name"" = @n)",
                @"DELETE FROM ""InventoryItems"" WHERE ""Name"" = @n"
            })
            {
                await using var cmd = owner.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("n", itemName);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
