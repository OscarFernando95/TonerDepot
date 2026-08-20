using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Exceptions;
using Toner.Application.Maintenance;
using Toner.Application.Maintenance.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Maintenance;

public class MaintenanceOrderServiceTests
{
    [Fact]
    public async Task CompleteAsync_GeneralOnly_RecalculatesGeneralAndRecordsReading()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(asset, contract, nextConsumablesDueCounter: 500_000);
        var order = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Asignada);
        arrangeDb.AddRange(client, contract, brand, model, asset, schedule, order);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceOrderService(actDb, new MaintenanceScheduleEngine(actDb));
        var before = DateTime.UtcNow;

        var result = await service.CompleteAsync(order.Id, new CompleteMaintenanceOrderRequest { CounterValue = 20000 }, Guid.NewGuid());

        Assert.Equal(nameof(MaintenanceOrderStatus.Completada), result.Status);
        Assert.NotNull(result.CompletedAt);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedSchedule = await assertDb.MaintenanceSchedules.SingleAsync(s => s.Id == schedule.Id);
        Assert.Equal(20000, updatedSchedule.LastGeneralMaintenanceCounter);
        Assert.Equal(20000 + 30000, updatedSchedule.NextGeneralDueCounter); // default de política: 30.000
        Assert.InRange(updatedSchedule.NextGeneralDueAt, before.AddMonths(6).AddMinutes(-1), before.AddMonths(6).AddMinutes(1));
        // No incluía cambio de insumos: esa sub-regla queda intacta.
        Assert.Equal(500_000, updatedSchedule.NextConsumablesDueCounter);

        var reading = await assertDb.MeterReadings.SingleAsync(m => m.AssetId == asset.Id);
        Assert.Equal(20000, reading.CounterValue);
    }

    [Fact]
    public async Task CompleteAsync_IncludesConsumablesOnly_AlsoResetsUnits()
    {
        // Cambiar insumos cuenta también como mantenimiento de unidades, aunque la orden no haya marcado
        // IncludesUnits explícitamente.
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(asset, contract);
        var order = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Asignada, includesConsumables: true);
        arrangeDb.AddRange(client, contract, brand, model, asset, schedule, order);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceOrderService(actDb, new MaintenanceScheduleEngine(actDb));

        await service.CompleteAsync(order.Id, new CompleteMaintenanceOrderRequest { CounterValue = 30000 }, Guid.NewGuid());

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedSchedule = await assertDb.MaintenanceSchedules.SingleAsync(s => s.Id == schedule.Id);
        Assert.Equal(30000, updatedSchedule.LastConsumablesChangeCounter);
        Assert.Equal(30000 + 60000, updatedSchedule.NextConsumablesDueCounter); // default de política: 60.000
        Assert.Equal(30000, updatedSchedule.LastUnitsMaintenanceCounter);
        Assert.Equal(30000 + 30000, updatedSchedule.NextUnitsDueCounter);
    }

    [Fact]
    public async Task CompleteAsync_CounterLessThanLastReading_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(asset, contract);
        var order = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Asignada);
        var reading = new MeterReading { AssetId = asset.Id, CounterValue = 20000, ReadingDate = DateTime.UtcNow };
        arrangeDb.AddRange(client, contract, brand, model, asset, schedule, order, reading);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceOrderService(actDb, new MaintenanceScheduleEngine(actDb));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CompleteAsync(order.Id, new CompleteMaintenanceOrderRequest { CounterValue = 10000 }, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(MaintenanceOrderStatus.Completada)]
    [InlineData(MaintenanceOrderStatus.Cancelada)]
    public async Task CompleteAsync_AlreadyClosed_Throws(MaintenanceOrderStatus status)
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(asset, contract);
        var order = TestEntities.MaintenanceOrder(schedule, asset, status);
        arrangeDb.AddRange(client, contract, brand, model, asset, schedule, order);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceOrderService(actDb, new MaintenanceScheduleEngine(actDb));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CompleteAsync(order.Id, new CompleteMaintenanceOrderRequest { CounterValue = 100 }, Guid.NewGuid()));
    }

    [Fact]
    public async Task CancelAsync_AlreadyCompleted_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(asset, contract);
        var order = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Completada);
        arrangeDb.AddRange(client, contract, brand, model, asset, schedule, order);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceOrderService(actDb, new MaintenanceScheduleEngine(actDb));

        await Assert.ThrowsAsync<ConflictException>(() => service.CancelAsync(order.Id));
    }

    [Fact]
    public async Task AssignAsync_FromEnProceso_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(asset, contract);
        var order = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.EnProceso);
        arrangeDb.AddRange(client, contract, brand, model, asset, schedule, order);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceOrderService(actDb, new MaintenanceScheduleEngine(actDb));

        await Assert.ThrowsAsync<ConflictException>(() => service.AssignAsync(
            order.Id,
            new AssignMaintenanceOrderRequest { TechnicianId = Guid.NewGuid() },
            Guid.NewGuid()));
    }

    [Fact]
    public async Task ListInCoverageAsync_ReturnsOthersOrdersInCoveredCity_ButExcludesOwnAndOtherCities()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);

        var coveredCity = TestEntities.City("Bogotá");
        var coveredLocation = TestEntities.ClientLocation(client, coveredCity);

        var otherCity = TestEntities.City("Medellín", "Antioquia");
        var otherLocation = TestEntities.ClientLocation(client, otherCity, "Sede Medellín");

        var techRole = TestEntities.Role(RoleNames.Tecnico);

        var viewerUser = TestEntities.User(techRole);
        var viewerTechnician = TestEntities.Technician(viewerUser);
        var coverage = TestEntities.Coverage(viewerTechnician, coveredCity);

        var ownerUser = TestEntities.User(techRole);
        var ownerTechnician = TestEntities.Technician(ownerUser);

        // Orden de otro técnico en la ciudad cubierta: debe verse.
        var assetInCoveredCity = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, coveredLocation.Id);
        var scheduleInCoveredCity = TestEntities.MaintenanceSchedule(assetInCoveredCity, contract);
        var orderInCoveredCity = TestEntities.MaintenanceOrder(scheduleInCoveredCity, assetInCoveredCity, MaintenanceOrderStatus.Asignada, ownerTechnician.Id);

        // Orden propia en la misma ciudad: no debe verse (ya aparece en "Mis órdenes").
        var ownAsset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, coveredLocation.Id);
        var ownSchedule = TestEntities.MaintenanceSchedule(ownAsset, contract);
        var ownOrder = TestEntities.MaintenanceOrder(ownSchedule, ownAsset, MaintenanceOrderStatus.Asignada, viewerTechnician.Id);

        // Orden de otro técnico en una ciudad no cubierta: no debe verse.
        var assetInOtherCity = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, otherLocation.Id);
        var scheduleInOtherCity = TestEntities.MaintenanceSchedule(assetInOtherCity, contract);
        var orderInOtherCity = TestEntities.MaintenanceOrder(scheduleInOtherCity, assetInOtherCity, MaintenanceOrderStatus.Asignada, ownerTechnician.Id);

        arrangeDb.AddRange(
            brand, model, client, contract, coveredCity, coveredLocation, otherCity, otherLocation,
            techRole, viewerUser, viewerTechnician, coverage, ownerUser, ownerTechnician,
            assetInCoveredCity, scheduleInCoveredCity, orderInCoveredCity,
            ownAsset, ownSchedule, ownOrder,
            assetInOtherCity, scheduleInOtherCity, orderInOtherCity);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceOrderService(actDb, new MaintenanceScheduleEngine(actDb));

        var result = (await service.ListInCoverageAsync(viewerTechnician.Id, null, null)).Items;

        var item = Assert.Single(result);
        Assert.Equal(orderInCoveredCity.Id, item.Id);
        Assert.Equal(ownerTechnician.Id, item.TechnicianId);
    }

    [Fact]
    public async Task ClaimAsync_AssignedToOtherTechnicianButNotStarted_ReassignsAndCreatesHistory()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(asset, contract);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var ownerUser = TestEntities.User(techRole);
        var owner = TestEntities.Technician(ownerUser);
        var claimingUser = TestEntities.User(techRole);
        var claimingTechnician = TestEntities.Technician(claimingUser);
        var order = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Asignada, owner.Id);
        arrangeDb.AddRange(
            client, contract, brand, model, asset, schedule,
            techRole, ownerUser, owner, claimingUser, claimingTechnician, order);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceOrderService(actDb, new MaintenanceScheduleEngine(actDb));

        var result = await service.ClaimAsync(order.Id, claimingTechnician.Id);

        Assert.Equal(nameof(MaintenanceOrderStatus.Asignada), result.Status);
        Assert.Equal(claimingTechnician.Id, result.TechnicianId);

        var history = (await service.GetAssignmentHistoryAsync(order.Id, null, null)).Items;
        var entry = Assert.Single(history);
        Assert.Equal(nameof(AssignmentType.Reclamada), entry.AssignmentType);
    }

    [Fact]
    public async Task ClaimAsync_AlreadyEnProceso_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(asset, contract);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var ownerUser = TestEntities.User(techRole);
        var owner = TestEntities.Technician(ownerUser);
        var claimingUser = TestEntities.User(techRole);
        var claimingTechnician = TestEntities.Technician(claimingUser);
        var order = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.EnProceso, owner.Id);
        arrangeDb.AddRange(
            client, contract, brand, model, asset, schedule,
            techRole, ownerUser, owner, claimingUser, claimingTechnician, order);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceOrderService(actDb, new MaintenanceScheduleEngine(actDb));

        await Assert.ThrowsAsync<ConflictException>(() => service.ClaimAsync(order.Id, claimingTechnician.Id));
    }
}
