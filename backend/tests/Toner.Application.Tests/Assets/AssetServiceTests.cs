using Microsoft.EntityFrameworkCore;
using Toner.Application.Assets;
using Toner.Application.Assets.Dtos;
using Toner.Application.Assignment;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Maintenance;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Assets;

public class AssetServiceTests
{
    [Fact]
    public async Task ChangeStatusAsync_FirstInstall_WithoutClientLocationId_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.EnBodega);
        arrangeDb.AssetBrands.Add(brand);
        arrangeDb.AssetModels.Add(model);
        arrangeDb.Assets.Add(asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.ChangeStatusAsync(
            asset.Id,
            new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.Instalado) },
            Guid.NewGuid()));

        Assert.Contains("ClientLocationId", ex.Message);
    }

    [Fact]
    public async Task ChangeStatusAsync_FirstInstall_WithClientLocationId_SetsLocationAndLogsChange()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.EnBodega);
        arrangeDb.AddRange(brand, model, city, client, location, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));
        var changedBy = Guid.NewGuid();

        var result = await service.ChangeStatusAsync(
            asset.Id,
            new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.Instalado), ClientLocationId = location.Id, Area = "Recepción" },
            changedBy);

        Assert.Equal(nameof(AssetLifecycleStatus.Instalado), result.LifecycleStatus);
        Assert.Equal(location.Id, result.CurrentClientLocationId);
        Assert.Equal("Recepción", result.Area);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var log = await assertDb.AssetStatusLogs.SingleAsync(l => l.AssetId == asset.Id);
        Assert.Equal(AssetLifecycleStatus.EnBodega, log.PreviousStatus);
        Assert.Equal(AssetLifecycleStatus.Instalado, log.NewStatus);
        Assert.Equal(changedBy, log.ChangedByUserId);
    }

    [Fact]
    public async Task ChangeStatusAsync_ReinstallAfterMaintenance_WithoutNewLocation_KeepsPreviousLocation()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.EnMantenimiento, location.Id);
        arrangeDb.AddRange(brand, model, city, client, location, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));

        var result = await service.ChangeStatusAsync(
            asset.Id,
            new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.Instalado), Area = "Contabilidad" },
            Guid.NewGuid());

        Assert.Equal(location.Id, result.CurrentClientLocationId);
    }

    [Fact]
    public async Task ChangeStatusAsync_ToEnBodega_ClearsCurrentLocationAndArea()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        asset.Area = "Recepción";
        arrangeDb.AddRange(brand, model, city, client, location, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));

        var result = await service.ChangeStatusAsync(
            asset.Id,
            new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.EnBodega) },
            Guid.NewGuid());

        Assert.Null(result.CurrentClientLocationId);
        Assert.Null(result.Area);
    }

    [Fact]
    public async Task ChangeStatusAsync_ToEnBodega_DeactivatesMaintenanceSchedule()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var contract = TestEntities.Contract(client);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        var schedule = TestEntities.MaintenanceSchedule(asset, contract, isActive: true);
        arrangeDb.AddRange(brand, model, city, client, location, contract, asset, schedule);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));

        await service.ChangeStatusAsync(asset.Id, new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.EnBodega) }, Guid.NewGuid());

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedSchedule = await assertDb.MaintenanceSchedules.SingleAsync(s => s.Id == schedule.Id);
        Assert.False(updatedSchedule.IsActive);
    }

    [Fact]
    public async Task ChangeStatusAsync_EnBodegaToPendienteInstalacion_RequiresClientLocationId()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.EnBodega);
        arrangeDb.AddRange(brand, model, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.ChangeStatusAsync(
            asset.Id,
            new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.PendienteInstalacion) },
            Guid.NewGuid()));

        Assert.Contains("ClientLocationId", ex.Message);
    }

    [Fact]
    public async Task ChangeStatusAsync_EnBodegaToPendienteInstalacion_SetsLocationAndClearsStaleArea()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.EnBodega);
        // Área "residual" de una instalación anterior (no debería pasar en la práctica, pero confirma
        // que la transición la limpia igual sin depender de que nunca ocurra).
        asset.Area = "Área vieja";
        arrangeDb.AddRange(brand, model, city, client, location, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));

        var result = await service.ChangeStatusAsync(
            asset.Id,
            new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.PendienteInstalacion), ClientLocationId = location.Id },
            Guid.NewGuid());

        Assert.Equal(nameof(AssetLifecycleStatus.PendienteInstalacion), result.LifecycleStatus);
        Assert.Equal(location.Id, result.CurrentClientLocationId);
        Assert.Null(result.Area);
    }

    [Fact]
    public async Task ChangeStatusAsync_PendienteInstalacionToInstalado_RequiresArea_ReusesLocation()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.PendienteInstalacion, location.Id);
        arrangeDb.AddRange(brand, model, city, client, location, asset);
        await arrangeDb.SaveChangesAsync();

        using (var missingAreaDb = TonerTestDb.CreateContext(dbName))
        {
            var missingAreaService = new AssetService(missingAreaDb, new MaintenanceScheduleEngine(missingAreaDb), new AssignmentEngine(missingAreaDb));
            var ex = await Assert.ThrowsAsync<ConflictException>(() => missingAreaService.ChangeStatusAsync(
                asset.Id,
                new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.Instalado) },
                Guid.NewGuid()));
            Assert.Contains("área", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));

        // No se especifica ClientLocationId de nuevo: debe reusar la sede que ya quedó asociada al
        // pasar por PendienteInstalacion, igual que la reinstalación tras mantenimiento.
        var result = await service.ChangeStatusAsync(
            asset.Id,
            new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.Instalado), Area = "Contabilidad" },
            Guid.NewGuid());

        Assert.Equal(location.Id, result.CurrentClientLocationId);
        Assert.Equal("Contabilidad", result.Area);
    }

    [Fact]
    public async Task PrepareStatusChangeAsync_MutatesButDoesNotSave_UntilCallerSaves()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.EnBodega);
        arrangeDb.AddRange(brand, model, city, client, location, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));

        var prepared = await service.PrepareStatusChangeAsync(
            asset.Id,
            new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.PendienteInstalacion), ClientLocationId = location.Id },
            Guid.NewGuid());

        Assert.Equal(AssetLifecycleStatus.PendienteInstalacion, prepared.LifecycleStatus);

        // No se llamó SaveChangesAsync todavía: un contexto nuevo no debe ver el cambio.
        using (var beforeSaveDb = TonerTestDb.CreateContext(dbName))
        {
            var stillEnBodega = await beforeSaveDb.Assets.SingleAsync(a => a.Id == asset.Id);
            Assert.Equal(AssetLifecycleStatus.EnBodega, stillEnBodega.LifecycleStatus);
        }

        await actDb.SaveChangesAsync();

        using var afterSaveDb = TonerTestDb.CreateContext(dbName);
        var updated = await afterSaveDb.Assets.SingleAsync(a => a.Id == asset.Id);
        Assert.Equal(AssetLifecycleStatus.PendienteInstalacion, updated.LifecycleStatus);
    }

    [Theory]
    [InlineData(AssetLifecycleStatus.EnBodega, AssetLifecycleStatus.EnMantenimiento)]
    [InlineData(AssetLifecycleStatus.DadoDeBaja, AssetLifecycleStatus.EnBodega)]
    [InlineData(AssetLifecycleStatus.DadoDeBaja, AssetLifecycleStatus.Instalado)]
    [InlineData(AssetLifecycleStatus.DadoDeBaja, AssetLifecycleStatus.PendienteInstalacion)]
    [InlineData(AssetLifecycleStatus.Instalado, AssetLifecycleStatus.PendienteInstalacion)]
    [InlineData(AssetLifecycleStatus.EnMantenimiento, AssetLifecycleStatus.PendienteInstalacion)]
    public async Task ChangeStatusAsync_InvalidTransition_Throws(AssetLifecycleStatus from, AssetLifecycleStatus to)
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, from);
        arrangeDb.AddRange(brand, model, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));

        await Assert.ThrowsAsync<ConflictException>(() => service.ChangeStatusAsync(
            asset.Id,
            new ChangeAssetStatusRequest { NewStatus = to.ToString() },
            Guid.NewGuid()));
    }

    [Fact]
    public async Task AddMeterReadingAsync_LowerThanLastReading_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        arrangeDb.AddRange(brand, model, asset);
        await arrangeDb.SaveChangesAsync();

        using (var seedReadingDb = TonerTestDb.CreateContext(dbName))
        {
            var service = new AssetService(seedReadingDb, new MaintenanceScheduleEngine(seedReadingDb), new AssignmentEngine(seedReadingDb));
            await service.AddMeterReadingAsync(asset.Id, new CreateMeterReadingRequest { CounterValue = 1000 }, StaffUser());
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var actService = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));

        await Assert.ThrowsAsync<ConflictException>(() => actService.AddMeterReadingAsync(
            asset.Id,
            new CreateMeterReadingRequest { CounterValue = 999 },
            StaffUser()));
    }

    [Fact]
    public async Task AddMeterReadingAsync_EqualToLastReading_IsAllowed()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        arrangeDb.AddRange(brand, model, asset);
        await arrangeDb.SaveChangesAsync();

        using (var seedReadingDb = TonerTestDb.CreateContext(dbName))
        {
            var service = new AssetService(seedReadingDb, new MaintenanceScheduleEngine(seedReadingDb), new AssignmentEngine(seedReadingDb));
            await service.AddMeterReadingAsync(asset.Id, new CreateMeterReadingRequest { CounterValue = 1000 }, StaffUser());
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var actService = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));

        var result = await actService.AddMeterReadingAsync(
            asset.Id,
            new CreateMeterReadingRequest { CounterValue = 1000 },
            StaffUser());

        Assert.Equal(1000, result.CounterValue);
    }

    private static RequestingUser StaffUser() => new(Guid.NewGuid(), RoleNames.Administrador, null, null);

    [Fact]
    public async Task ListPendingInstallationsAsync_OnlyReturnsAssetsInTechnicianCoverage()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var client = TestEntities.Client();

        var coveredCity = TestEntities.City("Bogotá");
        var coveredLocation = TestEntities.ClientLocation(client, coveredCity);
        var coveredAsset = TestEntities.Asset(model, AssetLifecycleStatus.PendienteInstalacion, coveredLocation.Id);

        var otherCity = TestEntities.City("Medellín", "Antioquia");
        var otherLocation = TestEntities.ClientLocation(client, otherCity, "Sede Medellín");
        var otherAsset = TestEntities.Asset(model, AssetLifecycleStatus.PendienteInstalacion, otherLocation.Id);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser);
        var coverage = TestEntities.Coverage(technician, coveredCity);

        arrangeDb.AddRange(
            brand, model, client, coveredCity, coveredLocation, coveredAsset,
            otherCity, otherLocation, otherAsset, techRole, techUser, technician, coverage);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));

        var result = await service.ListPendingInstallationsAsync(technician.Id);

        var item = Assert.Single(result);
        Assert.Equal(coveredAsset.Id, item.AssetId);
    }

    [Fact]
    public async Task ListPendingInstallationsAsync_TechnicianWithNoCoverage_ReturnsEmpty()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var client = TestEntities.Client();
        var city = TestEntities.City();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.PendienteInstalacion, location.Id);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser);

        arrangeDb.AddRange(brand, model, client, city, location, asset, techRole, techUser, technician);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));

        var result = await service.ListPendingInstallationsAsync(technician.Id);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ChangeStatusAsync_BackToEnBodega_EndsActiveContractAssetLink()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var client = TestEntities.Client();
        var city = TestEntities.City();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        var contract = TestEntities.Contract(client);
        var link = TestEntities.ContractAsset(contract, asset);

        arrangeDb.AddRange(brand, model, client, city, location, asset, contract, link);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));

        await service.ChangeStatusAsync(
            asset.Id,
            new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.EnBodega) },
            Guid.NewGuid());

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedLink = await assertDb.ContractAssets.SingleAsync(ca => ca.Id == link.Id);
        Assert.NotNull(updatedLink.EndDate);
        var updatedAsset = await assertDb.Assets.SingleAsync(a => a.Id == asset.Id);
        Assert.Equal(AssetLifecycleStatus.EnBodega, updatedAsset.LifecycleStatus);
        Assert.Null(updatedAsset.CurrentClientLocationId);
    }

    [Fact]
    public async Task ListAsync_AssetWithActiveContractLink_ReturnsActiveContractId()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var client = TestEntities.Client();
        var city = TestEntities.City();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        var oldContract = TestEntities.Contract(client);
        var activeContract = TestEntities.Contract(client);
        var endedLink = TestEntities.ContractAsset(oldContract, asset, endDate: DateTime.UtcNow.AddDays(-1));
        var activeLink = TestEntities.ContractAsset(activeContract, asset);

        arrangeDb.AddRange(brand, model, client, city, location, asset, oldContract, activeContract, endedLink, activeLink);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));

        var result = await service.GetByIdAsync(new RequestingUser(Guid.NewGuid(), RoleNames.Administrador, null, null), asset.Id);

        Assert.Equal(activeContract.Id, result.ActiveContractId);
    }

    [Fact]
    public async Task ListAsync_AssetWithNoActiveContractLink_ReturnsNullActiveContractId()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.EnBodega);
        arrangeDb.AddRange(brand, model, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));

        var result = await service.GetByIdAsync(new RequestingUser(Guid.NewGuid(), RoleNames.Administrador, null, null), asset.Id);

        Assert.Null(result.ActiveContractId);
    }

    [Fact]
    public async Task GetByIdAsync_ClienteConClientIdNulo_Deniega()
    {
        // SECURITY_AUDIT.md hallazgo #20: un ClientId nulo debe denegar explícitamente, no colar
        // como si el activo no estuviera instalado en ninguna sede (que hoy da el mismo 403, pero
        // por una razón implícita distinta).
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var client = TestEntities.Client();
        var city = TestEntities.City();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        arrangeDb.AddRange(brand, model, client, city, location, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb, new MaintenanceScheduleEngine(actDb), new AssignmentEngine(actDb));
        var malformedClientUser = new RequestingUser(Guid.NewGuid(), RoleNames.Cliente, null, null);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetByIdAsync(malformedClientUser, asset.Id));
    }

    [Fact]
    public async Task ListAsync_ClienteConClientIdNulo_Deniega()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(db, new MaintenanceScheduleEngine(db), new AssignmentEngine(db));
        var malformedClientUser = new RequestingUser(Guid.NewGuid(), RoleNames.Cliente, null, null);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ListAsync(malformedClientUser));
    }
}
