using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Exceptions;
using Toner.Application.Inventory;
using Toner.Application.Inventory.Dtos;
using Toner.Application.Inventory.Validators;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Zones;
using Toner.Application.Zones.Dtos;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Inventory;

public class InventoryServiceTests
{
    private sealed record Setup(string DbName, Guid MainId, Guid ZoneLocationId, Guid ItemId, Guid UserId);

    private static InventoryService Service(Infrastructure.Persistence.TonerDbContext db) => new(db);

    private static async Task<Setup> SeedAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        // La zona crea su ubicación al crearse, igual que en producción.
        var zone = await new ZoneService(db).CreateAsync(new CreateZoneRequest { Name = "Sur" });
        var main = new InventoryLocation { Kind = InventoryLocationKind.Principal, Name = "Bodega principal" };
        var item = new InventoryItem { Name = "Fusor", Category = InventoryCategory.ConsumibleBase, MinimumStock = 2 };
        db.AddRange(main, item);
        await db.SaveChangesAsync();
        var zoneLocation = await db.InventoryLocations.SingleAsync(l => l.ZoneId == zone.Id);
        return new Setup(dbName, main.Id, zoneLocation.Id, item.Id, Guid.NewGuid());
    }

    [Fact]
    public async Task CrearUnaZona_CreaSuUbicacionDeInventario_YEliminarlaLaQuita()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var zones = new ZoneService(db);

        var zone = await zones.CreateAsync(new CreateZoneRequest { Name = "Norte" });

        var location = await db.InventoryLocations.SingleAsync();
        Assert.Equal(zone.Id, location.ZoneId);
        Assert.Equal(InventoryLocationKind.Zona, location.Kind);

        await zones.DeleteAsync(zone.Id);
        Assert.Empty(db.InventoryLocations);
    }

    [Fact]
    public async Task ZonaConMovimientos_NoSePuedeEliminar()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        await Service(db).RegisterEntryAsync(new RegisterEntryRequest { LocationId = s.ZoneLocationId, ItemId = s.ItemId, Quantity = 3 }, s.UserId);
        var zoneId = (await db.Zones.SingleAsync()).Id;

        await Assert.ThrowsAsync<ConflictException>(() => new ZoneService(db).DeleteAsync(zoneId));
    }

    [Fact]
    public async Task Entrada_SumaAlSaldo_YSeVeEnElStock()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = Service(db);

        await service.RegisterEntryAsync(new RegisterEntryRequest { LocationId = s.MainId, ItemId = s.ItemId, Quantity = 10, Notes = "Compra" }, s.UserId);
        await service.RegisterEntryAsync(new RegisterEntryRequest { LocationId = s.MainId, ItemId = s.ItemId, Quantity = 5 }, s.UserId);

        var stock = (await service.ListStockAsync(null, null, null, false, null, null)).Items;
        var row = Assert.Single(stock);
        Assert.Equal(15, row.Quantity);
        Assert.Equal("Bodega principal", row.LocationName);
        Assert.False(row.IsLow);
    }

    [Fact]
    public async Task Traspaso_MueveDeUnaUbicacionAOtra_ConSusDosFilas()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = Service(db);
        await service.RegisterEntryAsync(new RegisterEntryRequest { LocationId = s.MainId, ItemId = s.ItemId, Quantity = 10 }, s.UserId);

        var moves = await service.TransferAsync(new TransferRequest { ItemId = s.ItemId, FromLocationId = s.MainId, ToLocationId = s.ZoneLocationId, Quantity = 4 }, s.UserId);

        Assert.Equal(new[] { -4, 4 }, moves.Select(m => m.Delta).OrderBy(d => d));
        var stock = (await service.ListStockAsync(null, null, null, false, null, null)).Items;
        Assert.Equal(6, stock.Single(r => r.LocationId == s.MainId).Quantity);
        Assert.Equal(4, stock.Single(r => r.LocationId == s.ZoneLocationId).Quantity);
        Assert.Equal(2, await db.InventoryMovements.CountAsync(m => m.TransferId != null));
    }

    [Fact]
    public async Task Traspaso_SinSaldoSuficiente_SeRechazaYNoDejaMovimientos()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = Service(db);
        await service.RegisterEntryAsync(new RegisterEntryRequest { LocationId = s.MainId, ItemId = s.ItemId, Quantity = 3 }, s.UserId);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.TransferAsync(new TransferRequest { ItemId = s.ItemId, FromLocationId = s.MainId, ToLocationId = s.ZoneLocationId, Quantity = 4 }, s.UserId));

        Assert.Contains("Saldo insuficiente", ex.Message);
        Assert.Equal(1, await db.InventoryMovements.CountAsync());
    }

    [Fact]
    public async Task Ajuste_PuedeRestar_YMarcaStockBajoOCero()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = Service(db);
        await service.RegisterEntryAsync(new RegisterEntryRequest { LocationId = s.MainId, ItemId = s.ItemId, Quantity = 5 }, s.UserId);

        await service.AdjustAsync(new AdjustStockRequest { LocationId = s.MainId, ItemId = s.ItemId, Delta = -3, Notes = "Conteo físico" }, s.UserId);

        // Mínimo 2, saldo 2 → bajo.
        var row = Assert.Single((await service.ListStockAsync(null, null, null, false, null, null)).Items);
        Assert.Equal(2, row.Quantity);
        Assert.True(row.IsLow);
        Assert.Single((await service.ListStockAsync(null, null, null, onlyLow: true, null, null)).Items);
    }

    [Fact]
    public async Task ItemDesactivado_NoAdmiteEntradasNiTraspasos()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = Service(db);
        var item = await db.InventoryItems.SingleAsync();
        await service.UpdateItemAsync(item.Id, new UpdateInventoryItemRequest { Name = item.Name, Category = "ConsumibleBase", MinimumStock = 2, IsActive = false });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RegisterEntryAsync(new RegisterEntryRequest { LocationId = s.MainId, ItemId = s.ItemId, Quantity = 1 }, s.UserId));
    }

    [Fact]
    public async Task Catalogo_RechazaNombresRepetidosSinImportarMayusculas_YFiltraPorCategoria()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = Service(db);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateItemAsync(new CreateInventoryItemRequest { Name = " fusor ", Category = "Repuesto" }));
        await service.CreateItemAsync(new CreateInventoryItemRequest { Name = "Empaque", Category = "Repuesto", UnitCost = 1500m });

        var repuestos = await service.ListItemsAsync(null, "Repuesto", false, null, null);
        Assert.Equal(new[] { "Empaque" }, repuestos.Items.Select(i => i.Name));
        Assert.Single((await service.ListItemsAsync("fus", null, false, null, null)).Items);
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => service.ListItemsAsync(null, "Nada", false, null, null));
    }

    [Fact]
    public async Task SedePrincipal_SeEditaConNombreDireccionYCiudad()
    {
        var s = await SeedAsync();
        var city = TestEntities.City("Neiva", "Huila");
        using (var arrange = TonerTestDb.CreateContext(s.DbName))
        {
            arrange.Add(city);
            await arrange.SaveChangesAsync();
        }

        using var db = TonerTestDb.CreateContext(s.DbName);
        var main = await Service(db).UpdateMainLocationAsync(new UpdateMainLocationRequest { Name = "Sede Neiva", Address = "Calle 1 # 2-3", CityId = city.Id });

        Assert.Equal("Sede Neiva", main.Name);
        Assert.Equal("Neiva", main.CityName);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service(db).UpdateMainLocationAsync(new UpdateMainLocationRequest { Name = "X", CityId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task Movimientos_SePaginanPorCursor_DelMasNuevoAlMasViejo()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = Service(db);
        for (var i = 1; i <= 5; i++)
        {
            await service.RegisterEntryAsync(new RegisterEntryRequest { LocationId = s.MainId, ItemId = s.ItemId, Quantity = i }, s.UserId);
        }

        var first = await service.ListMovementsAsync(null, null, null, 2);
        Assert.Equal(2, first.Items.Count);
        Assert.NotNull(first.NextCursor);
        var second = await service.ListMovementsAsync(null, null, first.NextCursor, 2);
        Assert.Empty(first.Items.Select(m => m.Id).Intersect(second.Items.Select(m => m.Id)));
    }

    [Fact]
    public void Validadores_RechazanLoInvalido()
    {
        var item = Guid.NewGuid();
        var a = Guid.NewGuid();
        Assert.False(new RegisterEntryRequestValidator().Validate(new RegisterEntryRequest { LocationId = a, ItemId = item, Quantity = 0 }).IsValid);
        Assert.False(new TransferRequestValidator().Validate(new TransferRequest { ItemId = item, FromLocationId = a, ToLocationId = a, Quantity = 1 }).IsValid);
        Assert.False(new AdjustStockRequestValidator().Validate(new AdjustStockRequest { LocationId = a, ItemId = item, Delta = 0, Notes = "x" }).IsValid);
        Assert.False(new AdjustStockRequestValidator().Validate(new AdjustStockRequest { LocationId = a, ItemId = item, Delta = 1, Notes = "" }).IsValid);
        Assert.False(new CreateInventoryItemRequestValidator().Validate(new CreateInventoryItemRequest { Name = "X", Category = "Cualquiera" }).IsValid);
        Assert.True(new CreateInventoryItemRequestValidator().Validate(new CreateInventoryItemRequest { Name = "X", Category = "Toner" }).IsValid);
    }
}
