using Microsoft.EntityFrameworkCore;
using Toner.Application.Maintenance;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Maintenance;

public class MaintenanceScheduleServiceTests
{
    [Fact]
    public async Task ListAsync_ProjectsClientContractAndLastKnownCounter()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        var schedule = TestEntities.MaintenanceSchedule(asset, contract);
        var reading = new Domain.Entities.MeterReading { AssetId = asset.Id, CounterValue = 15000, ReadingDate = DateTime.UtcNow };
        arrangeDb.AddRange(city, client, location, contract, brand, model, asset, schedule, reading);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceScheduleService(actDb, new MaintenanceScheduleEngine(actDb));

        var result = (await service.ListAsync(null, null)).Items;

        var item = Assert.Single(result);
        Assert.Equal(client.Id, item.ClientId);
        Assert.Equal(contract.Id, item.ContractId);
        Assert.Equal(city.Name, item.CityName);
        Assert.Equal(15000, item.LastKnownCounter);
    }

    [Fact]
    public async Task ListAsync_ComputesAreaLastAndNextMaintenanceSummaries()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        asset.Area = "Recepción";

        // General a 3.000 impresiones de vencer (dentro de cualquier ventana), unidades muy lejos,
        // insumos muy lejos — general debería liderar y emparejarse con lo más próximo de los otros dos.
        var lastAt = DateTime.UtcNow.AddDays(-10);
        var schedule = TestEntities.MaintenanceSchedule(
            asset, contract,
            nextGeneralDueAt: DateTime.UtcNow.AddMonths(6), nextGeneralDueCounter: 30000,
            nextUnitsDueAt: DateTime.UtcNow.AddYears(5), nextUnitsDueCounter: 10_000_000,
            nextConsumablesDueCounter: 10_000_000,
            lastGeneralMaintenanceAt: lastAt, lastGeneralMaintenanceCounter: 20000,
            lastUnitsMaintenanceAt: lastAt, lastUnitsMaintenanceCounter: 20000,
            lastConsumablesChangeAt: lastAt.AddDays(-1), lastConsumablesChangeCounter: 15000);
        var reading = new Domain.Entities.MeterReading { AssetId = asset.Id, CounterValue = 27000, ReadingDate = DateTime.UtcNow };
        arrangeDb.AddRange(city, client, location, contract, brand, model, asset, schedule, reading);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceScheduleService(actDb, new MaintenanceScheduleEngine(actDb));

        var result = (await service.ListAsync(null, null)).Items;

        var item = Assert.Single(result);
        Assert.Equal("Recepción", item.Area);
        Assert.Equal(lastAt, item.LastMaintenanceAt);
        Assert.Equal(new[] { "MG", "MU" }, item.LastMaintenanceCodes);
        // General lidera (mucho más cerca proporcionalmente) y se empareja con unidades, que está
        // proporcionalmente más cerca que insumos.
        Assert.Equal(new[] { "MG", "MU" }, item.NextMaintenanceCodes);
        Assert.Equal(schedule.NextGeneralDueAt, item.NextMaintenanceAt);
        Assert.Equal(schedule.NextGeneralDueCounter, item.NextMaintenanceCounter);
    }

    [Fact]
    public async Task SetActiveStatusAsync_UpdatesFlag()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(asset, contract);
        arrangeDb.AddRange(client, contract, brand, model, asset, schedule);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceScheduleService(actDb, new MaintenanceScheduleEngine(actDb));

        var result = await service.SetActiveStatusAsync(schedule.Id, false);

        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task ListInCoverageAsync_OnlyReturnsSchedulesInCoveredCity()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);

        var coveredCity = TestEntities.City("Bogotá");
        var coveredLocation = TestEntities.ClientLocation(client, coveredCity);
        var coveredAsset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, coveredLocation.Id);
        var coveredSchedule = TestEntities.MaintenanceSchedule(coveredAsset, contract);

        var otherCity = TestEntities.City("Medellín", "Antioquia");
        var otherLocation = TestEntities.ClientLocation(client, otherCity, "Sede Medellín");
        var otherAsset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, otherLocation.Id);
        var otherSchedule = TestEntities.MaintenanceSchedule(otherAsset, contract);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser);
        var coverage = TestEntities.Coverage(technician, coveredCity);

        arrangeDb.AddRange(
            client, contract, brand, model, coveredCity, coveredLocation, coveredAsset, coveredSchedule,
            otherCity, otherLocation, otherAsset, otherSchedule, techRole, techUser, technician, coverage);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceScheduleService(actDb, new MaintenanceScheduleEngine(actDb));

        var result = (await service.ListInCoverageAsync(technician.Id, null, null)).Items;

        var item = Assert.Single(result);
        Assert.Equal(coveredSchedule.Id, item.Id);
    }

    [Fact]
    public async Task BackfillMissingAsync_CreatesScheduleForInstalledAssetWithActiveContractAndNoSchedule()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        var link = TestEntities.ContractAsset(contract, asset);
        var reading = new Domain.Entities.MeterReading { AssetId = asset.Id, CounterValue = 8000, ReadingDate = DateTime.UtcNow };
        arrangeDb.AddRange(city, client, location, contract, brand, model, asset, link, reading);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceScheduleService(actDb, new MaintenanceScheduleEngine(actDb));

        var created = await service.BackfillMissingAsync();

        Assert.Equal(1, created);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var schedule = await assertDb.MaintenanceSchedules.SingleAsync(s => s.AssetId == asset.Id);
        Assert.Equal(contract.Id, schedule.ContractId);
        Assert.Equal(8000, schedule.LastGeneralMaintenanceCounter);
    }

    [Fact]
    public async Task BackfillMissingAsync_AssetInEnBodega_IsSkipped()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.EnBodega);
        arrangeDb.AddRange(brand, model, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceScheduleService(actDb, new MaintenanceScheduleEngine(actDb));

        var created = await service.BackfillMissingAsync();

        Assert.Equal(0, created);
    }
}
