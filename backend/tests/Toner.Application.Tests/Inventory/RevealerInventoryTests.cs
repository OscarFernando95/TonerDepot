using Microsoft.EntityFrameworkCore;
using Toner.Application.Assets;
using Toner.Application.Common.Exceptions;
using Toner.Application.Inventory;
using Toner.Application.Inventory.Dtos;
using Toner.Application.Inventory.Validators;
using Toner.Application.Maintenance;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Inventory;

// Revelador: se maneja en gramos. La compra entra en bolsas (× gramos por bolsa), el kit del modelo fija los gramos
// por cambio y el técnico no los ajusta; el saldo nunca queda en negativo.
public class RevealerInventoryTests
{
    private sealed record Setup(string DbName, Guid MainId, Guid AssetId, Guid RevealerId, Guid UserId);

    private static InventoryService Inventory(Infrastructure.Persistence.TonerDbContext db) => new(db, new PassThroughStockLock());

    private static InventoryConsumptionService Consumption(Infrastructure.Persistence.TonerDbContext db) =>
        new(db, new BaseKitService(db), new PassThroughStockLock(), new MeterReadingRecorder(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db)));

    // Equipo instalado con un kit de marca que lleva 180 g de revelador por cambio y una bolsa de 500 g.
    private static async Task<Setup> SeedAsync(int kitGrams = 180)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var main = new InventoryLocation { Kind = InventoryLocationKind.Principal, Name = "Bodega principal" };
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var brand = TestEntities.AssetBrand("Ricoh");
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        asset.ClientId = client.Id;
        var revealer = new InventoryItem { Name = "Revelador MP2552", Category = InventoryCategory.Revelador, Unit = "g", GramsPerBag = 500 };
        db.AddRange(main, city, client, location, brand, model, asset, revealer);
        await db.SaveChangesAsync();

        await new BaseKitService(db).SetBrandKitAsync(brand.Id, new SetBrandKitRequest
        {
            Items = new[] { new BaseKitItemRequest { ItemId = revealer.Id, GroupName = "Unidad de revelado", Quantity = kitGrams } }
        });
        return new Setup(dbName, main.Id, asset.Id, revealer.Id, Guid.NewGuid());
    }

    private static VisitConsumption Visit(Setup s) => new(null, s.AssetId, null, null, null, null, s.UserId);

    [Fact]
    public async Task CrearRevelador_FijaLaUnidadEnGramos_YExigeLosGramosPorBolsa()
    {
        using var db = TonerTestDb.CreateContext(Guid.NewGuid().ToString());
        var service = Inventory(db);

        var created = await service.CreateItemAsync(new CreateInventoryItemRequest
        {
            Name = "Revelador SP8300", Category = "Revelador", Unit = "und", GramsPerBag = 1000
        });

        Assert.Equal("g", created.Unit);
        Assert.Equal(1000, created.GramsPerBag);

        var validator = new CreateInventoryItemRequestValidator();
        Assert.False(validator.Validate(new CreateInventoryItemRequest { Name = "X", Category = "Revelador" }).IsValid);
        Assert.False(validator.Validate(new CreateInventoryItemRequest { Name = "X", Category = "Repuesto", GramsPerBag = 500 }).IsValid);
        Assert.True(validator.Validate(new CreateInventoryItemRequest { Name = "X", Category = "Revelador", GramsPerBag = 500 }).IsValid);
    }

    [Fact]
    public async Task Entrada_DeRevelador_ConvierteLasBolsasAGramos()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = Inventory(db);

        var movement = await service.RegisterEntryAsync(new RegisterEntryRequest { LocationId = s.MainId, ItemId = s.RevealerId, Quantity = 2 }, s.UserId);

        Assert.Equal(1000, movement.Delta);
        Assert.Contains("2 bolsa(s) de 500 g", movement.Notes);
        var row = Assert.Single((await service.ListStockAsync(null, s.RevealerId, null, false, null, null)).Items);
        Assert.Equal(1000, row.Quantity);
        Assert.Equal(500, row.GramsPerBag);
    }

    [Fact]
    public async Task CambiarUnItemConHistoriaDeOtraCategoriaAReveladorOViceversa_SeRechaza()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var fusor = new InventoryItem { Name = "Fusor", Category = InventoryCategory.ConsumibleBase };
        db.Add(fusor);
        await db.SaveChangesAsync();
        var service = Inventory(db);
        await service.RegisterEntryAsync(new RegisterEntryRequest { LocationId = s.MainId, ItemId = fusor.Id, Quantity = 3 }, s.UserId);

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateItemAsync(fusor.Id, new UpdateInventoryItemRequest
        {
            Name = "Fusor", Category = "Revelador", GramsPerBag = 500
        }));
        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateItemAsync(s.RevealerId, new UpdateInventoryItemRequest
        {
            Name = "Revelador MP2552", Category = "Repuesto"
        }));
    }

    [Fact]
    public async Task Kit_AdmiteGramosEnElRevelador_PeroNoMasDe100UnidadesEnOtrasPiezas()
    {
        var s = await SeedAsync(kitGrams: 2500);
        using var db = TonerTestDb.CreateContext(s.DbName);
        var brandId = (await db.AssetBrands.SingleAsync()).Id;
        var fusor = new InventoryItem { Name = "Fusor", Category = InventoryCategory.ConsumibleBase };
        db.Add(fusor);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => new BaseKitService(db).SetBrandKitAsync(brandId, new SetBrandKitRequest
        {
            Items = new[] { new BaseKitItemRequest { ItemId = fusor.Id, GroupName = "Unidad fusora", Quantity = 150 } }
        }));
    }

    [Fact]
    public async Task VisitaConRevelador_DescuentaLosGramosDelKit()
    {
        var s = await SeedAsync(kitGrams: 180);
        using var db = TonerTestDb.CreateContext(s.DbName);
        await Inventory(db).RegisterEntryAsync(new RegisterEntryRequest { LocationId = s.MainId, ItemId = s.RevealerId, Quantity = 1 }, s.UserId);

        await Consumption(db).PrepareVisitConsumptionAsync(Visit(s), new[] { new UsedPartRequest { ItemId = s.RevealerId, Quantity = 180 } });
        await db.SaveChangesAsync();

        Assert.Equal(500 - 180, await db.InventoryMovements.Where(m => m.InventoryItemId == s.RevealerId).SumAsync(m => m.Delta));
    }

    [Fact]
    public async Task VisitaConRevelador_ConUnGramajeDistintoAlDelKit_SeRechaza()
    {
        var s = await SeedAsync(kitGrams: 180);
        using var db = TonerTestDb.CreateContext(s.DbName);
        await Inventory(db).RegisterEntryAsync(new RegisterEntryRequest { LocationId = s.MainId, ItemId = s.RevealerId, Quantity = 1 }, s.UserId);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Consumption(db).PrepareVisitConsumptionAsync(Visit(s), new[] { new UsedPartRequest { ItemId = s.RevealerId, Quantity = 100 } }));

        Assert.Contains("180 g", ex.Message);
    }

    [Fact]
    public async Task VisitaConRevelador_SinSaldoSuficienteEnGramos_SeRechaza()
    {
        var s = await SeedAsync(kitGrams: 180);
        using var db = TonerTestDb.CreateContext(s.DbName);
        await Inventory(db).AdjustAsync(new AdjustStockRequest { LocationId = s.MainId, ItemId = s.RevealerId, Delta = 150, Notes = "conteo" }, s.UserId);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Consumption(db).PrepareVisitConsumptionAsync(Visit(s), new[] { new UsedPartRequest { ItemId = s.RevealerId, Quantity = 180 } }));

        Assert.Contains("hay 150 g, necesitas 180 g", ex.Message);
        Assert.Equal(150, await db.InventoryMovements.Where(m => m.InventoryItemId == s.RevealerId).SumAsync(m => m.Delta));
    }

    [Fact]
    public async Task BusquedaLibreDePiezas_NoOfreceElRevelador()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        var found = (await Consumption(db).SearchPartsAsync(s.AssetId, "Revelador", null, null)).Items;

        Assert.Empty(found);
    }
}
