using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Exceptions;
using Toner.Application.Maintenance;
using Toner.Application.Maintenance.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Maintenance;

public class MaintenanceOrderServiceTests
{
    [Fact]
    public async Task CompleteAsync_PorTiempoSchedule_RecalculatesNextDueAtFromIntervalDays()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand);
        var schedule = TestEntities.MaintenanceSchedule(asset, MaintenanceFrequencyType.PorTiempo, timeIntervalDays: 90);
        var order = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Asignada);
        arrangeDb.AddRange(brand, asset, schedule, order);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceOrderService(actDb);
        var before = DateTime.UtcNow;

        var result = await service.CompleteAsync(order.Id);

        Assert.Equal(nameof(MaintenanceOrderStatus.Completada), result.Status);
        Assert.NotNull(result.CompletedAt);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedSchedule = await assertDb.MaintenanceSchedules.SingleAsync(s => s.Id == schedule.Id);
        Assert.NotNull(updatedSchedule.LastExecutedAt);
        Assert.InRange(updatedSchedule.NextDueAt!.Value, before.AddDays(90).AddMinutes(-1), before.AddDays(90).AddMinutes(1));
    }

    [Fact]
    public async Task CompleteAsync_PorContadorSchedule_WithReading_RecalculatesFromLastReading()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand);
        var schedule = TestEntities.MaintenanceSchedule(asset, MaintenanceFrequencyType.PorContador, printThreshold: 5000);
        var order = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Asignada);
        var reading = new MeterReading { AssetId = asset.Id, CounterValue = 20000, ReadingDate = DateTime.UtcNow };
        arrangeDb.AddRange(brand, asset, schedule, order, reading);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceOrderService(actDb);

        await service.CompleteAsync(order.Id);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedSchedule = await assertDb.MaintenanceSchedules.SingleAsync(s => s.Id == schedule.Id);
        Assert.Equal(20000, updatedSchedule.LastExecutedCounter);
        Assert.Equal(25000, updatedSchedule.NextDueCounter);
    }

    [Fact]
    public async Task CompleteAsync_PorContadorSchedule_NoReadings_BaselinesFromZero()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand);
        var schedule = TestEntities.MaintenanceSchedule(asset, MaintenanceFrequencyType.PorContador, printThreshold: 5000);
        var order = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Asignada);
        arrangeDb.AddRange(brand, asset, schedule, order);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceOrderService(actDb);

        await service.CompleteAsync(order.Id);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedSchedule = await assertDb.MaintenanceSchedules.SingleAsync(s => s.Id == schedule.Id);
        Assert.Null(updatedSchedule.LastExecutedCounter);
        Assert.Equal(5000, updatedSchedule.NextDueCounter);
    }

    [Theory]
    [InlineData(MaintenanceOrderStatus.Completada)]
    [InlineData(MaintenanceOrderStatus.Cancelada)]
    public async Task CompleteAsync_AlreadyClosed_Throws(MaintenanceOrderStatus status)
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand);
        var schedule = TestEntities.MaintenanceSchedule(asset, MaintenanceFrequencyType.PorTiempo, timeIntervalDays: 90);
        var order = TestEntities.MaintenanceOrder(schedule, asset, status);
        arrangeDb.AddRange(brand, asset, schedule, order);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceOrderService(actDb);

        await Assert.ThrowsAsync<ConflictException>(() => service.CompleteAsync(order.Id));
    }

    [Fact]
    public async Task CancelAsync_AlreadyCompleted_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand);
        var schedule = TestEntities.MaintenanceSchedule(asset, MaintenanceFrequencyType.PorTiempo, timeIntervalDays: 90);
        var order = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Completada);
        arrangeDb.AddRange(brand, asset, schedule, order);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceOrderService(actDb);

        await Assert.ThrowsAsync<ConflictException>(() => service.CancelAsync(order.Id));
    }

    [Fact]
    public async Task AssignAsync_FromEnProceso_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand);
        var schedule = TestEntities.MaintenanceSchedule(asset, MaintenanceFrequencyType.PorTiempo, timeIntervalDays: 90);
        var order = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.EnProceso);
        arrangeDb.AddRange(brand, asset, schedule, order);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceOrderService(actDb);

        await Assert.ThrowsAsync<ConflictException>(() => service.AssignAsync(
            order.Id,
            new AssignMaintenanceOrderRequest { TechnicianId = Guid.NewGuid() },
            Guid.NewGuid()));
    }
}
