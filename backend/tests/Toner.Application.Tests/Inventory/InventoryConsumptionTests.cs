using Microsoft.EntityFrameworkCore;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Inventory;
using Toner.Application.Inventory.Dtos;
using Toner.Application.Inventory.Validators;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Inventory;

// Consumo de inventario en el campo: las piezas de una visita salen del inventario de la zona del equipo, el kit base
// llega al técnico con el saldo de esa zona, y el tóner se registra por máquina.
public class InventoryConsumptionTests
{
    private sealed record Scenario(
        string DbName, Guid TechnicianId, Guid TechnicianUserId, Guid TicketId, Guid AssetId, Guid ClientId, Guid ZoneLocationId, Guid MainId,
        Guid Fusor, Guid Presor, Guid Repuesto, Guid TonerId);

    // Equipo instalado en un municipio de una zona con inventario; ticket abierto en visita; kit Ricoh de dos piezas; tóner.
    private static async Task<Scenario> SeedAsync(bool cityHasZone = true)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var zone = new Zone { Name = "Sur" };
        var zoneLocation = new InventoryLocation { Kind = InventoryLocationKind.Zona, Zone = zone };
        var main = new InventoryLocation { Kind = InventoryLocationKind.Principal, Name = "Bodega principal" };
        var city = TestEntities.City();
        if (cityHasZone) city.ZoneId = zone.Id;
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var brand = TestEntities.AssetBrand("Ricoh");
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        asset.ClientId = client.Id;
        var reporterRole = TestEntities.Role(RoleNames.Cliente);
        var reporter = TestEntities.User(reporterRole);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);
        var ticket = TestEntities.ServiceTicket(location, reporter, ServiceTicketStatus.Asignado, technicianId: technician.Id);
        ticket.ClientId = client.Id;
        ticket.AssetId = asset.Id;
        var fusor = new InventoryItem { Name = "Fusor", Category = InventoryCategory.ConsumibleBase };
        var presor = new InventoryItem { Name = "Presor", Category = InventoryCategory.ConsumibleBase };
        var repuesto = new InventoryItem { Name = "Empaque", Category = InventoryCategory.Repuesto };
        var toner = new InventoryItem { Name = "Tóner negro", Category = InventoryCategory.Toner };

        db.AddRange(zone, zoneLocation, main, city, client, location, brand, model, asset, reporterRole, reporter, techRole, techUser, technician, ticket, fusor, presor, repuesto, toner);
        db.Add(new TechnicianZone { TechnicianId = technician.Id, ZoneId = zone.Id });
        db.Add(new BrandBaseItem { AssetBrandId = brand.Id, InventoryItemId = fusor.Id, GroupName = "Unidad fusora", Quantity = 1 });
        db.Add(new BrandBaseItem { AssetBrandId = brand.Id, InventoryItemId = presor.Id, GroupName = "Unidad fusora", Quantity = 2 });
        await db.SaveChangesAsync();
        return new Scenario(dbName, technician.Id, techUser.Id, ticket.Id, asset.Id, client.Id, zoneLocation.Id, main.Id, fusor.Id, presor.Id, repuesto.Id, toner.Id);
    }

    private static async Task StockAsync(Scenario s, Guid locationId, Guid itemId, int quantity)
    {
        using var db = TonerTestDb.CreateContext(s.DbName);
        db.InventoryMovements.Add(new InventoryMovement
        {
            InventoryItemId = itemId, InventoryLocationId = locationId, Type = InventoryMovementType.Entrada, Delta = quantity
        });
        await db.SaveChangesAsync();
    }

    private static async Task CheckInAsync(Scenario s)
    {
        using var db = TonerTestDb.CreateContext(s.DbName);
        await TestCheckIn.Create(db).CheckInAsync(s.TechnicianId, new CheckInRequest { ServiceTicketId = s.TicketId });
    }

    private static async Task<TechnicianSelfStatusDto> CheckOutAsync(Scenario s, params (Guid item, int qty)[] parts)
    {
        using var db = TonerTestDb.CreateContext(s.DbName);
        return await TestCheckIn.Create(db).CheckOutAsync(s.TechnicianId, new CheckOutRequest
        {
            Resolved = true,
            InitialCounterValue = 15000,
            Parts = parts.Select(p => new UsedPartRequest { ItemId = p.item, Quantity = p.qty }).ToList()
        });
    }

    [Fact]
    public async Task CheckOut_ConPiezas_DescuentaDelInventarioDeLaZonaDelEquipo_YLasLigaALaVisita()
    {
        var s = await SeedAsync();
        await StockAsync(s, s.ZoneLocationId, s.Fusor, 5);
        await StockAsync(s, s.ZoneLocationId, s.Repuesto, 3);
        await StockAsync(s, s.MainId, s.Fusor, 9);   // la principal no se toca
        await CheckInAsync(s);

        var status = await CheckOutAsync(s, (s.Fusor, 1), (s.Repuesto, 2));

        Assert.Empty(status.StockWarnings);
        using var db = TonerTestDb.CreateContext(s.DbName);
        var consumption = await db.InventoryMovements.Where(m => m.Type == InventoryMovementType.Consumo).ToListAsync();
        Assert.Equal(2, consumption.Count);
        Assert.All(consumption, m =>
        {
            Assert.Equal(s.ZoneLocationId, m.InventoryLocationId);
            Assert.Equal(s.ClientId, m.ClientId);
            Assert.Equal(s.AssetId, m.AssetId);
            Assert.Equal(s.TicketId, m.ServiceTicketId);
            Assert.NotNull(m.TimeLogId);
            Assert.Equal(15000, m.CounterValue);
            Assert.Equal(s.TechnicianUserId, m.CreatedByUserId);
        });
        Assert.Equal(4, await db.InventoryMovements.Where(m => m.InventoryLocationId == s.ZoneLocationId && m.InventoryItemId == s.Fusor).SumAsync(m => m.Delta));
        Assert.Equal(1, await db.InventoryMovements.Where(m => m.InventoryLocationId == s.ZoneLocationId && m.InventoryItemId == s.Repuesto).SumAsync(m => m.Delta));
        Assert.Equal(9, await db.InventoryMovements.Where(m => m.InventoryLocationId == s.MainId && m.InventoryItemId == s.Fusor).SumAsync(m => m.Delta));
    }

    [Fact]
    public async Task CheckOut_SinStockSuficiente_CierraLaVisitaYAvisaDelNegativo()
    {
        var s = await SeedAsync();
        await StockAsync(s, s.ZoneLocationId, s.Fusor, 1);
        await CheckInAsync(s);

        var status = await CheckOutAsync(s, (s.Fusor, 3));

        var warning = Assert.Single(status.StockWarnings);
        Assert.Contains("Fusor", warning);
        Assert.Contains("-2", warning);
        using var db = TonerTestDb.CreateContext(s.DbName);
        Assert.Equal(ServiceTicketStatus.Resuelto, (await db.ServiceTickets.SingleAsync()).Status);
        Assert.Equal(-2, await db.InventoryMovements.Where(m => m.InventoryLocationId == s.ZoneLocationId).SumAsync(m => m.Delta));
    }

    [Fact]
    public async Task CheckOut_SiElMunicipioNoTieneZona_DescuentaDeLaBodegaPrincipal()
    {
        var s = await SeedAsync(cityHasZone: false);
        await StockAsync(s, s.MainId, s.Fusor, 4);
        await CheckInAsync(s);

        await CheckOutAsync(s, (s.Fusor, 1));

        using var db = TonerTestDb.CreateContext(s.DbName);
        var m = await db.InventoryMovements.SingleAsync(x => x.Type == InventoryMovementType.Consumo);
        Assert.Equal(s.MainId, m.InventoryLocationId);
        Assert.Contains("bodega principal", m.Notes);
    }

    [Fact]
    public async Task CheckOut_SumaLasPiezasRepetidas_YRechazaPiezasInvalidas()
    {
        var s = await SeedAsync();
        await StockAsync(s, s.ZoneLocationId, s.Fusor, 9);
        await CheckInAsync(s);

        await Assert.ThrowsAsync<ConflictException>(() => CheckOutAsync(s, (Guid.NewGuid(), 1)));
        await Assert.ThrowsAsync<ConflictException>(() => CheckOutAsync(s, (s.Fusor, 0)));

        await CheckOutAsync(s, (s.Fusor, 1), (s.Fusor, 2));

        using var db = TonerTestDb.CreateContext(s.DbName);
        var m = await db.InventoryMovements.SingleAsync(x => x.Type == InventoryMovementType.Consumo);
        Assert.Equal(-3, m.Delta);
    }

    [Fact]
    public async Task CheckOut_ConPiezasRechazadas_NoCierraLaVisita()
    {
        var s = await SeedAsync();
        await CheckInAsync(s);

        await Assert.ThrowsAsync<ConflictException>(() => CheckOutAsync(s, (Guid.NewGuid(), 1)));

        using var db = TonerTestDb.CreateContext(s.DbName);
        Assert.Null((await db.TimeLogs.SingleAsync()).EndTime);
        Assert.Empty(db.InventoryMovements);
    }

    [Fact]
    public async Task Kit_DelEquipo_TraeSusPiezasConElSaldoDeLaZona_SinLasExcluidas()
    {
        var s = await SeedAsync();
        await StockAsync(s, s.ZoneLocationId, s.Fusor, 7);
        using (var arrange = TonerTestDb.CreateContext(s.DbName))
        {
            var modelId = (await arrange.Assets.SingleAsync()).AssetModelId;
            arrange.ModelBaseItems.Add(new ModelBaseItem { AssetModelId = modelId, InventoryItemId = s.Presor, Excluded = true });
            await arrange.SaveChangesAsync();
        }

        using var db = TonerTestDb.CreateContext(s.DbName);
        var kit = await new InventoryConsumptionService(db, new BaseKitService(db)).GetKitForAssetAsync(s.AssetId);

        Assert.Equal("Sur", kit.LocationName);
        Assert.False(kit.UsesMainWarehouse);
        var item = Assert.Single(kit.Items);
        Assert.Equal("Fusor", item.ItemName);
        Assert.Equal(7, item.Stock);
    }

    [Fact]
    public async Task Kit_SinEquipoCatalogado_EsVacio_YUsaLaBodegaPrincipal()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        var kit = await new InventoryConsumptionService(db, new BaseKitService(db)).GetKitForAssetAsync(null);

        Assert.Empty(kit.Items);
        Assert.True(kit.UsesMainWarehouse);
    }

    [Fact]
    public async Task BuscarRepuestos_FiltraPorNombre_ConElSaldoDeLaZona_YSinInactivos()
    {
        var s = await SeedAsync();
        await StockAsync(s, s.ZoneLocationId, s.Repuesto, 6);
        using (var arrange = TonerTestDb.CreateContext(s.DbName))
        {
            (await arrange.InventoryItems.SingleAsync(i => i.Id == s.Presor)).IsActive = false;
            await arrange.SaveChangesAsync();
        }

        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = new InventoryConsumptionService(db, new BaseKitService(db));

        var found = (await service.SearchPartsAsync(s.AssetId, "empa", null, null)).Items;
        Assert.Equal(6, Assert.Single(found).Stock);
        Assert.DoesNotContain((await service.SearchPartsAsync(s.AssetId, null, null, null)).Items, p => p.Name == "Presor");
        Assert.Equal(new[] { "Tóner negro" }, (await service.SearchPartsAsync(s.AssetId, null, null, null, category: "Toner")).Items.Select(p => p.Name));
    }

    private static RequestingUser Tech(Scenario s) => new(s.TechnicianUserId, RoleNames.Tecnico, null, s.TechnicianId);

    [Fact]
    public async Task Toner_UnTecnicoLoRegistraSoloEnSusMaquinasVinculadas()
    {
        var s = await SeedAsync();
        await StockAsync(s, s.ZoneLocationId, s.TonerId, 5);
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = new InventoryConsumptionService(db, new BaseKitService(db));
        var request = new RegisterTonerRequest { AssetId = s.AssetId, ItemId = s.TonerId, Quantity = 2, DeliveredToUser = true };

        await Assert.ThrowsAsync<ForbiddenException>(() => service.RegisterTonerAsync(request, Tech(s)));

        db.TechnicianAssets.Add(new TechnicianAsset { TechnicianId = s.TechnicianId, AssetId = s.AssetId });
        await db.SaveChangesAsync();
        var entry = await service.RegisterTonerAsync(request, Tech(s));

        Assert.Equal(2, entry.Quantity);
        Assert.Contains("Entregado al usuario", entry.Notes);
        Assert.Null(entry.StockWarning);
        var movement = await db.InventoryMovements.SingleAsync(m => m.Type == InventoryMovementType.Consumo);
        Assert.Equal(s.ZoneLocationId, movement.InventoryLocationId);
        Assert.Equal(s.ClientId, movement.ClientId);
        Assert.Equal(s.AssetId, movement.AssetId);
    }

    [Fact]
    public async Task Toner_UsaLaFechaIndicada_PorDefectoAhora_YListaElHistorial()
    {
        var s = await SeedAsync();
        await StockAsync(s, s.ZoneLocationId, s.TonerId, 9);
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = new InventoryConsumptionService(db, new BaseKitService(db));
        var staff = new RequestingUser(Guid.NewGuid(), RoleNames.Administrador, null, null);
        var past = DateTime.UtcNow.AddDays(-20);

        await service.RegisterTonerAsync(new RegisterTonerRequest { AssetId = s.AssetId, ItemId = s.TonerId, Quantity = 1, OccurredAt = past, CounterValue = 1000 }, staff);
        await service.RegisterTonerAsync(new RegisterTonerRequest { AssetId = s.AssetId, ItemId = s.TonerId, Quantity = 1 }, staff);

        var all = (await service.ListTonerAsync(s.AssetId, null, null, null, null, staff)).Items;
        Assert.Equal(2, all.Count);
        Assert.True(all[0].OccurredAt > all[1].OccurredAt);                    // el más nuevo primero
        Assert.Equal(1000, all[1].CounterValue);
        Assert.InRange(all[0].OccurredAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));

        var recent = (await service.ListTonerAsync(s.AssetId, DateTime.UtcNow.AddDays(-5), null, null, null, staff)).Items;
        Assert.Single(recent);
    }

    [Fact]
    public async Task Toner_RechazaItemQueNoEsToner_YAvisaDeStockNegativo()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = new InventoryConsumptionService(db, new BaseKitService(db));
        var staff = new RequestingUser(Guid.NewGuid(), RoleNames.Coordinador, null, null);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RegisterTonerAsync(new RegisterTonerRequest { AssetId = s.AssetId, ItemId = s.Fusor, Quantity = 1 }, staff));

        var entry = await service.RegisterTonerAsync(new RegisterTonerRequest { AssetId = s.AssetId, ItemId = s.TonerId, Quantity = 1 }, staff);
        Assert.Contains("Stock insuficiente", entry.StockWarning);
    }

    [Fact]
    public void ValidadorDeToner_RechazaCantidadFechaFuturaYContadorNegativo()
    {
        var v = new RegisterTonerRequestValidator();
        var ok = new RegisterTonerRequest { AssetId = Guid.NewGuid(), ItemId = Guid.NewGuid(), Quantity = 1 };

        Assert.True(v.Validate(ok).IsValid);
        Assert.False(v.Validate(new RegisterTonerRequest { AssetId = ok.AssetId, ItemId = ok.ItemId, Quantity = 0 }).IsValid);
        Assert.False(v.Validate(new RegisterTonerRequest { AssetId = ok.AssetId, ItemId = ok.ItemId, Quantity = 1, OccurredAt = DateTime.UtcNow.AddDays(2) }).IsValid);
        Assert.False(v.Validate(new RegisterTonerRequest { AssetId = ok.AssetId, ItemId = ok.ItemId, Quantity = 1, CounterValue = -1 }).IsValid);
    }
}
