using Microsoft.EntityFrameworkCore;
using Toner.Application.Maintenance;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Maintenance;

public class MaintenanceScheduleEngineTests
{
    // Fechas/contadores "lejos" usados para aislar cuál sub-regla es la que realmente está por vencer
    // en cada test, sin ambigüedad frente a la regla de "combinación forzada" con lo más próximo.
    private static readonly DateTime FarDate = DateTime.UtcNow.AddYears(5);
    private const long FarCounter = 10_000_000;

    [Fact]
    public async Task EvaluateAsync_NothingDueSoon_ReturnsNull()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(
            asset, contract,
            nextGeneralDueAt: FarDate, nextGeneralDueCounter: FarCounter,
            nextUnitsDueAt: FarDate, nextUnitsDueCounter: FarCounter,
            nextConsumablesDueCounter: FarCounter);
        arrangeDb.AddRange(client, contract, brand, model, asset, schedule);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new MaintenanceScheduleEngine(actDb);

        var order = await engine.EvaluateAsync(asset.Id, currentCounter: 0, DateTime.UtcNow);

        Assert.Null(order);
    }

    [Fact]
    public async Task EvaluateAsync_GeneralDueSoon_PairsWithCloserConsumables()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(
            asset, contract,
            nextGeneralDueAt: FarDate, nextGeneralDueCounter: 30000, // faltan 3000, <= 5000 de anticipación
            nextUnitsDueAt: FarDate, nextUnitsDueCounter: FarCounter, // unidades lejos
            nextConsumablesDueCounter: 60000); // insumos más cerca (proporcionalmente) que unidades
        arrangeDb.AddRange(client, contract, brand, model, asset, schedule);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new MaintenanceScheduleEngine(actDb);

        var order = await engine.EvaluateAsync(asset.Id, currentCounter: 27000, DateTime.UtcNow);

        Assert.NotNull(order);
        Assert.True(order!.IncludesGeneral);
        Assert.False(order.IncludesUnits);
        Assert.True(order.IncludesConsumables);
    }

    [Fact]
    public async Task EvaluateAsync_GeneralDueSoon_PairsWithCloserUnits()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(
            asset, contract,
            nextGeneralDueAt: FarDate, nextGeneralDueCounter: 30000, // faltan 3000
            nextUnitsDueAt: FarDate, nextUnitsDueCounter: 30500, // unidades más cerca que insumos
            nextConsumablesDueCounter: FarCounter); // insumos lejos
        arrangeDb.AddRange(client, contract, brand, model, asset, schedule);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new MaintenanceScheduleEngine(actDb);

        var order = await engine.EvaluateAsync(asset.Id, currentCounter: 27000, DateTime.UtcNow);

        Assert.NotNull(order);
        Assert.True(order!.IncludesGeneral);
        Assert.True(order.IncludesUnits);
        Assert.False(order.IncludesConsumables);
    }

    [Fact]
    public async Task EvaluateAsync_GeneralDueSoonByTime_CreatesOrder()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(
            asset, contract,
            nextGeneralDueAt: DateTime.UtcNow.AddDays(10), nextGeneralDueCounter: FarCounter, // dentro de la ventana de 15 días
            nextUnitsDueAt: FarDate, nextUnitsDueCounter: FarCounter,
            nextConsumablesDueCounter: FarCounter);
        arrangeDb.AddRange(client, contract, brand, model, asset, schedule);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new MaintenanceScheduleEngine(actDb);

        var order = await engine.EvaluateAsync(asset.Id, currentCounter: 0, DateTime.UtcNow);

        Assert.NotNull(order);
        Assert.True(order!.IncludesGeneral);
    }

    [Fact]
    public async Task EvaluateAsync_UnitsDueSoon_GeneralAndConsumablesFar_CreatesOrderWithUnitsOnly()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(
            asset, contract,
            nextGeneralDueAt: FarDate, nextGeneralDueCounter: FarCounter,
            nextUnitsDueAt: FarDate, nextUnitsDueCounter: 30000, // faltan 2000, <= 5000
            nextConsumablesDueCounter: FarCounter);
        arrangeDb.AddRange(client, contract, brand, model, asset, schedule);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new MaintenanceScheduleEngine(actDb);

        var order = await engine.EvaluateAsync(asset.Id, currentCounter: 28000, DateTime.UtcNow);

        Assert.NotNull(order);
        Assert.False(order!.IncludesGeneral);
        Assert.True(order.IncludesUnits);
        Assert.False(order.IncludesConsumables);
    }

    [Fact]
    public async Task EvaluateAsync_ConsumablesDueSoon_GeneralAndUnitsFar_CreatesOrderWithConsumablesOnly()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(
            asset, contract,
            nextGeneralDueAt: FarDate, nextGeneralDueCounter: FarCounter,
            nextUnitsDueAt: FarDate, nextUnitsDueCounter: FarCounter,
            nextConsumablesDueCounter: 60000);
        arrangeDb.AddRange(client, contract, brand, model, asset, schedule);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new MaintenanceScheduleEngine(actDb);

        // Faltan 2000 impresiones para insumos (<= 5000).
        var order = await engine.EvaluateAsync(asset.Id, currentCounter: 58000, DateTime.UtcNow);

        Assert.NotNull(order);
        Assert.False(order!.IncludesGeneral);
        Assert.False(order.IncludesUnits);
        Assert.True(order.IncludesConsumables);
    }

    [Fact]
    public async Task EvaluateAsync_AlreadyHasOpenOrder_DoesNotDuplicate()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(
            asset, contract,
            nextGeneralDueAt: DateTime.UtcNow, nextGeneralDueCounter: 100,
            nextUnitsDueAt: DateTime.UtcNow, nextUnitsDueCounter: 100,
            nextConsumablesDueCounter: 100);
        var existingOrder = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Asignada);
        arrangeDb.AddRange(client, contract, brand, model, asset, schedule, existingOrder);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new MaintenanceScheduleEngine(actDb);

        var order = await engine.EvaluateAsync(asset.Id, currentCounter: 100, DateTime.UtcNow);

        Assert.Null(order);
    }

    [Fact]
    public async Task UpsertForInstallationAsync_AllDone_StartsFromFullThresholds()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        arrangeDb.AddRange(client, contract, brand, model, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new MaintenanceScheduleEngine(actDb);
        var installDate = DateTime.UtcNow;

        var schedule = await engine.UpsertForInstallationAsync(
            asset.Id, contract.Id, 5000, installDate,
            generalMaintenanceDone: true, unitsMaintenanceDone: true, existingConsumablesPrints: 0);
        await actDb.SaveChangesAsync();

        Assert.Equal(5000 + 30000, schedule.NextGeneralDueCounter);
        Assert.Equal(5000 + 30000, schedule.NextUnitsDueCounter);
        Assert.Equal(5000 + 60000, schedule.NextConsumablesDueCounter);
    }

    [Fact]
    public async Task UpsertForInstallationAsync_GeneralNotDone_DueImmediately()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        arrangeDb.AddRange(client, contract, brand, model, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new MaintenanceScheduleEngine(actDb);
        var installDate = DateTime.UtcNow;

        var schedule = await engine.UpsertForInstallationAsync(
            asset.Id, contract.Id, 5000, installDate,
            generalMaintenanceDone: false, unitsMaintenanceDone: true, existingConsumablesPrints: 0);
        await actDb.SaveChangesAsync();

        Assert.Equal(installDate, schedule.NextGeneralDueAt);
        Assert.Equal(5000, schedule.NextGeneralDueCounter);
    }

    [Fact]
    public async Task UpsertForInstallationAsync_ConsumablesNew_AlsoResetsUnitsFresh()
    {
        // Instalar con insumos nuevos cuenta también como mantenimiento de unidades (los insumos viven
        // dentro de las unidades) — ambas sub-reglas arrancan juntas desde la instalación.
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        arrangeDb.AddRange(client, contract, brand, model, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new MaintenanceScheduleEngine(actDb);
        var installDate = DateTime.UtcNow;

        var schedule = await engine.UpsertForInstallationAsync(
            asset.Id, contract.Id, 105000, installDate,
            generalMaintenanceDone: true, unitsMaintenanceDone: false, existingConsumablesPrints: null);
        await actDb.SaveChangesAsync();

        Assert.Equal(installDate.AddMonths(6), schedule.NextUnitsDueAt);
        Assert.Equal(105000 + 30000, schedule.NextUnitsDueCounter);
        Assert.Equal(105000 + 60000, schedule.NextConsumablesDueCounter); // insumos nuevos: offset 0
    }

    [Fact]
    public async Task UpsertForInstallationAsync_UnitsDoneWithUsedConsumables_OffsetsThreshold()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        arrangeDb.AddRange(client, contract, brand, model, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new MaintenanceScheduleEngine(actDb);
        var installDate = DateTime.UtcNow;

        var schedule = await engine.UpsertForInstallationAsync(
            asset.Id, contract.Id, 5000, installDate,
            generalMaintenanceDone: true, unitsMaintenanceDone: true, existingConsumablesPrints: 20000);
        await actDb.SaveChangesAsync();

        Assert.Equal(5000 + (60000 - 20000), schedule.NextConsumablesDueCounter);
    }

    // CODE_QUALITY_AUDIT.md hallazgo #6: EvaluateAllDueAsync reemplaza el loop de ~3 consultas por
    // cronograma del job diario por un puñado de consultas en lote. Estos tests prueban que la
    // decisión por cronograma sigue siendo independiente y correcta cuando se evalúan varios a la vez
    // — no solo que el método compile.
    [Fact]
    public async Task EvaluateAllDueAsync_MultipleSchedules_EvaluatesEachIndependently()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);

        var dueAsset = TestEntities.Asset(model, locationId: null);
        var dueSchedule = TestEntities.MaintenanceSchedule(
            dueAsset, contract,
            nextGeneralDueAt: DateTime.UtcNow, nextGeneralDueCounter: FarCounter,
            nextUnitsDueAt: FarDate, nextUnitsDueCounter: FarCounter,
            nextConsumablesDueCounter: FarCounter);

        var farAsset = TestEntities.Asset(model, locationId: null);
        var farSchedule = TestEntities.MaintenanceSchedule(
            farAsset, contract,
            nextGeneralDueAt: FarDate, nextGeneralDueCounter: FarCounter,
            nextUnitsDueAt: FarDate, nextUnitsDueCounter: FarCounter,
            nextConsumablesDueCounter: FarCounter);

        arrangeDb.AddRange(client, contract, brand, model, dueAsset, dueSchedule, farAsset, farSchedule);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new MaintenanceScheduleEngine(actDb);

        var createdOrders = await engine.EvaluateAllDueAsync(DateTime.UtcNow);
        await actDb.SaveChangesAsync();

        var order = Assert.Single(createdOrders);
        Assert.Equal(dueSchedule.Id, order.MaintenanceScheduleId);
        Assert.True(order.IncludesGeneral);
    }

    [Fact]
    public async Task EvaluateAllDueAsync_AlreadyHasOpenOrder_SkipsThatScheduleOnly()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);

        var blockedAsset = TestEntities.Asset(model, locationId: null);
        var blockedSchedule = TestEntities.MaintenanceSchedule(
            blockedAsset, contract,
            nextGeneralDueAt: DateTime.UtcNow, nextGeneralDueCounter: FarCounter,
            nextUnitsDueAt: FarDate, nextUnitsDueCounter: FarCounter,
            nextConsumablesDueCounter: FarCounter);
        var existingOrder = TestEntities.MaintenanceOrder(blockedSchedule, blockedAsset, MaintenanceOrderStatus.Asignada);

        var freeAsset = TestEntities.Asset(model, locationId: null);
        var freeSchedule = TestEntities.MaintenanceSchedule(
            freeAsset, contract,
            nextGeneralDueAt: DateTime.UtcNow, nextGeneralDueCounter: FarCounter,
            nextUnitsDueAt: FarDate, nextUnitsDueCounter: FarCounter,
            nextConsumablesDueCounter: FarCounter);

        arrangeDb.AddRange(client, contract, brand, model, blockedAsset, blockedSchedule, existingOrder, freeAsset, freeSchedule);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new MaintenanceScheduleEngine(actDb);

        var createdOrders = await engine.EvaluateAllDueAsync(DateTime.UtcNow);

        var order = Assert.Single(createdOrders);
        Assert.Equal(freeSchedule.Id, order.MaintenanceScheduleId);
    }

    [Fact]
    public async Task EvaluateAllDueAsync_UsesLastMeterReading_ForCounterBasedRule()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, locationId: null);
        var schedule = TestEntities.MaintenanceSchedule(
            asset, contract,
            nextGeneralDueAt: FarDate, nextGeneralDueCounter: 30000, // faltan 3000 desde el contador real (27000)
            nextUnitsDueAt: FarDate, nextUnitsDueCounter: FarCounter,
            nextConsumablesDueCounter: FarCounter);
        var reading = new MeterReading { AssetId = asset.Id, ReadingDate = DateTime.UtcNow, CounterValue = 27000 };
        arrangeDb.AddRange(client, contract, brand, model, asset, schedule, reading);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new MaintenanceScheduleEngine(actDb);

        var createdOrders = await engine.EvaluateAllDueAsync(DateTime.UtcNow);

        var order = Assert.Single(createdOrders);
        Assert.True(order.IncludesGeneral);
    }
}
