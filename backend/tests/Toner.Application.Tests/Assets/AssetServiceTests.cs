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
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(brand, AssetLifecycleStatus.Instalado, location.Id);
        asset.Area = "Recepción";
        arrangeDb.AddRange(brand, city, client, location, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb);

        var result = await service.ChangeStatusAsync(
            asset.Id,
            new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.EnBodega) },
            Guid.NewGuid());

        Assert.Null(result.CurrentClientLocationId);
        Assert.Null(result.Area);
    }

    [Fact]
    public async Task ChangeStatusAsync_EnBodegaToPendienteInstalacion_RequiresClientLocationId()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand, AssetLifecycleStatus.EnBodega);
        arrangeDb.AddRange(brand, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb);

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
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(brand, AssetLifecycleStatus.EnBodega);
        // Área "residual" de una instalación anterior (no debería pasar en la práctica, pero confirma
        // que la transición la limpia igual sin depender de que nunca ocurra).
        asset.Area = "Área vieja";
        arrangeDb.AddRange(brand, city, client, location, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb);

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
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(brand, AssetLifecycleStatus.PendienteInstalacion, location.Id);
        arrangeDb.AddRange(brand, city, client, location, asset);
        await arrangeDb.SaveChangesAsync();

        using (var missingAreaDb = TonerTestDb.CreateContext(dbName))
        {
            var missingAreaService = new AssetService(missingAreaDb);
            var ex = await Assert.ThrowsAsync<ConflictException>(() => missingAreaService.ChangeStatusAsync(
                asset.Id,
                new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.Instalado) },
                Guid.NewGuid()));
            Assert.Contains("área", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb);

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
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(brand, AssetLifecycleStatus.EnBodega);
        arrangeDb.AddRange(brand, city, client, location, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new AssetService(actDb);

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
