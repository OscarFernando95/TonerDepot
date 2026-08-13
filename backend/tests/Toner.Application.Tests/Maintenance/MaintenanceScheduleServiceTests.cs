using Toner.Application.Common.Exceptions;
using Toner.Application.Maintenance;
using Toner.Application.Maintenance.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Maintenance;

public class MaintenanceScheduleServiceTests
{
    [Fact]
    public async Task CreateAsync_PorTiempo_SetsNextDueAtFromNowPlusInterval()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand);
        arrangeDb.AddRange(brand, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceScheduleService(actDb);
        var before = DateTime.UtcNow;

        var result = await service.CreateAsync(new CreateMaintenanceScheduleRequest
        {
            AssetId = asset.Id,
            FrequencyType = nameof(MaintenanceFrequencyType.PorTiempo),
            TimeIntervalDays = 90
        });

        Assert.NotNull(result.NextDueAt);
        Assert.InRange(result.NextDueAt!.Value, before.AddDays(90).AddMinutes(-1), before.AddDays(90).AddMinutes(1));
        Assert.Null(result.NextDueCounter);
    }

    [Fact]
    public async Task CreateAsync_PorContador_NoPriorReadings_UsesThresholdAsBaseline()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand);
        arrangeDb.AddRange(brand, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceScheduleService(actDb);

        var result = await service.CreateAsync(new CreateMaintenanceScheduleRequest
        {
            AssetId = asset.Id,
            FrequencyType = nameof(MaintenanceFrequencyType.PorContador),
            PrintThreshold = 5000
        });

        Assert.Equal(5000, result.NextDueCounter);
    }

    [Fact]
    public async Task CreateAsync_PorContador_WithPriorReading_AddsThresholdToLastReading()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand);
        var reading = new Domain.Entities.MeterReading { AssetId = asset.Id, CounterValue = 12000, ReadingDate = DateTime.UtcNow };
        arrangeDb.AddRange(brand, asset, reading);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceScheduleService(actDb);

        var result = await service.CreateAsync(new CreateMaintenanceScheduleRequest
        {
            AssetId = asset.Id,
            FrequencyType = nameof(MaintenanceFrequencyType.PorContador),
            PrintThreshold = 5000
        });

        Assert.Equal(17000, result.NextDueCounter);
    }

    [Fact]
    public async Task UpdateAsync_PorContadorSchedule_WithoutPrintThreshold_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand);
        var schedule = TestEntities.MaintenanceSchedule(asset, MaintenanceFrequencyType.PorContador, printThreshold: 5000);
        arrangeDb.AddRange(brand, asset, schedule);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new MaintenanceScheduleService(actDb);

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(
            schedule.Id,
            new UpdateMaintenanceScheduleRequest { TimeIntervalDays = 30 }));
    }
}
