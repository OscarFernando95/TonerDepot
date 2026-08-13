using Microsoft.EntityFrameworkCore;
using Toner.Application.Assets;
using Toner.Application.Common.Exceptions;
using Toner.Application.Contracts;
using Toner.Application.Contracts.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Contracts;

public class ContractAssetServiceTests
{
    [Fact]
    public async Task AddAsync_AssetInEnBodega_MovesToPendienteInstalacion_InSameTransactionAsLink()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand, AssetLifecycleStatus.EnBodega);
        arrangeDb.AddRange(city, client, location, contract, brand, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new ContractAssetService(actDb, new AssetService(actDb));
        var changedBy = Guid.NewGuid();

        var result = await service.AddAsync(
            contract.Id,
            new AddContractAssetRequest { AssetId = asset.Id, ClientLocationId = location.Id },
            changedBy);

        Assert.Equal(contract.Id, result.ContractId);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedAsset = await assertDb.Assets.SingleAsync(a => a.Id == asset.Id);
        Assert.Equal(AssetLifecycleStatus.PendienteInstalacion, updatedAsset.LifecycleStatus);
        Assert.Equal(location.Id, updatedAsset.CurrentClientLocationId);

        var log = await assertDb.AssetStatusLogs.SingleAsync(l => l.AssetId == asset.Id);
        Assert.Equal(AssetLifecycleStatus.EnBodega, log.PreviousStatus);
        Assert.Equal(AssetLifecycleStatus.PendienteInstalacion, log.NewStatus);
        Assert.Equal(changedBy, log.ChangedByUserId);

        var contractAsset = await assertDb.ContractAssets.SingleAsync(ca => ca.AssetId == asset.Id);
        Assert.Equal(contract.Id, contractAsset.ContractId);
    }

    [Fact]
    public async Task AddAsync_ClientLocationBelongsToDifferentClient_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var contractClient = TestEntities.Client("Cliente del contrato");
        var otherClient = TestEntities.Client("Otro cliente");
        var otherClientLocation = TestEntities.ClientLocation(otherClient, city);
        var contract = TestEntities.Contract(contractClient);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand, AssetLifecycleStatus.EnBodega);
        arrangeDb.AddRange(city, contractClient, otherClient, otherClientLocation, contract, brand, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new ContractAssetService(actDb, new AssetService(actDb));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.AddAsync(
            contract.Id,
            new AddContractAssetRequest { AssetId = asset.Id, ClientLocationId = otherClientLocation.Id },
            Guid.NewGuid()));

        Assert.Contains("no pertenece al cliente", ex.Message);
    }

    [Fact]
    public async Task AddAsync_AssetNotInEnBodega_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        // Ya instalado en otro lado — no debería poder "despacharse" a un contrato nuevo sin antes
        // volver a EnBodega.
        var asset = TestEntities.Asset(brand, AssetLifecycleStatus.Instalado, location.Id);
        arrangeDb.AddRange(city, client, location, contract, brand, asset);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new ContractAssetService(actDb, new AssetService(actDb));

        await Assert.ThrowsAsync<ConflictException>(() => service.AddAsync(
            contract.Id,
            new AddContractAssetRequest { AssetId = asset.Id, ClientLocationId = location.Id },
            Guid.NewGuid()));
    }

    [Fact]
    public async Task AddAsync_AssetAlreadyHasActiveLink_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var existingContract = TestEntities.Contract(client);
        var newContract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand, AssetLifecycleStatus.EnBodega);
        var existingLink = new ContractAsset
        {
            ContractId = existingContract.Id,
            AssetId = asset.Id,
            StartDate = DateTime.UtcNow
        };
        arrangeDb.AddRange(city, client, location, existingContract, newContract, brand, asset, existingLink);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new ContractAssetService(actDb, new AssetService(actDb));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.AddAsync(
            newContract.Id,
            new AddContractAssetRequest { AssetId = asset.Id, ClientLocationId = location.Id },
            Guid.NewGuid()));

        Assert.Contains("ya está vinculado", ex.Message);
    }
}
