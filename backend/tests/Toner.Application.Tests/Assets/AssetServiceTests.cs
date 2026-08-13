using Microsoft.EntityFrameworkCore;
using Toner.Application.Assets;
using Toner.Application.Assets.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Tests.TestSupport;
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
        var asset = TestEntities.Asset(brand, AssetLifecycleStatus.EnBodega);
        arrangeDb.AssetBrands.Add(brand);
        arrangeDb.Assets.Add(asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb);

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
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(brand, AssetLifecycleStatus.EnBodega);
        arrangeDb.AddRange(brand, city, client, location, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb);
        var changedBy = Guid.NewGuid();

        var result = await service.ChangeStatusAsync(
            asset.Id,
            new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.Instalado), ClientLocationId = location.Id },
            changedBy);

        Assert.Equal(nameof(AssetLifecycleStatus.Instalado), result.LifecycleStatus);
        Assert.Equal(location.Id, result.CurrentClientLocationId);

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
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(brand, AssetLifecycleStatus.EnMantenimiento, location.Id);
        arrangeDb.AddRange(brand, city, client, location, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb);

        var result = await service.ChangeStatusAsync(
            asset.Id,
            new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.Instalado) },
            Guid.NewGuid());

        Assert.Equal(location.Id, result.CurrentClientLocationId);
    }

    [Fact]
    public async Task ChangeStatusAsync_ToEnBodega_ClearsCurrentLocation()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(brand, AssetLifecycleStatus.Instalado, location.Id);
        arrangeDb.AddRange(brand, city, client, location, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb);

        var result = await service.ChangeStatusAsync(
            asset.Id,
            new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.EnBodega) },
            Guid.NewGuid());

        Assert.Null(result.CurrentClientLocationId);
    }

    [Theory]
    [InlineData(AssetLifecycleStatus.EnBodega, AssetLifecycleStatus.EnMantenimiento)]
    [InlineData(AssetLifecycleStatus.DadoDeBaja, AssetLifecycleStatus.EnBodega)]
    [InlineData(AssetLifecycleStatus.DadoDeBaja, AssetLifecycleStatus.Instalado)]
    public async Task ChangeStatusAsync_InvalidTransition_Throws(AssetLifecycleStatus from, AssetLifecycleStatus to)
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand, from);
        arrangeDb.AddRange(brand, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb);

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
        var asset = TestEntities.Asset(brand);
        arrangeDb.AddRange(brand, asset);
        await arrangeDb.SaveChangesAsync();

        using (var seedReadingDb = TonerTestDb.CreateContext(dbName))
        {
            var service = new AssetService(seedReadingDb);
            await service.AddMeterReadingAsync(asset.Id, new CreateMeterReadingRequest { CounterValue = 1000 }, Guid.NewGuid());
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var actService = new AssetService(actDb);

        await Assert.ThrowsAsync<ConflictException>(() => actService.AddMeterReadingAsync(
            asset.Id,
            new CreateMeterReadingRequest { CounterValue = 999 },
            Guid.NewGuid()));
    }

    [Fact]
    public async Task AddMeterReadingAsync_EqualToLastReading_IsAllowed()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand);
        arrangeDb.AddRange(brand, asset);
        await arrangeDb.SaveChangesAsync();

        using (var seedReadingDb = TonerTestDb.CreateContext(dbName))
        {
            var service = new AssetService(seedReadingDb);
            await service.AddMeterReadingAsync(asset.Id, new CreateMeterReadingRequest { CounterValue = 1000 }, Guid.NewGuid());
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var actService = new AssetService(actDb);

        var result = await actService.AddMeterReadingAsync(
            asset.Id,
            new CreateMeterReadingRequest { CounterValue = 1000 },
            Guid.NewGuid());

        Assert.Equal(1000, result.CounterValue);
    }
}
