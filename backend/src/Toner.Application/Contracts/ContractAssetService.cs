using Microsoft.EntityFrameworkCore;
using Toner.Application.Assets;
using Toner.Application.Assets.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Contracts.Dtos;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Contracts;

public class ContractAssetService : IContractAssetService
{
    private readonly IApplicationDbContext _db;
    private readonly IAssetService _assetService;

    public ContractAssetService(IApplicationDbContext db, IAssetService assetService)
    {
        _db = db;
        _assetService = assetService;
    }

    public async Task<ContractAssetDto> AddAsync(Guid contractId, AddContractAssetRequest request, Guid changedByUserId, CancellationToken cancellationToken = default)
    {
        var contract = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken)
            ?? throw new NotFoundException(nameof(Contract), contractId);

        var locationBelongsToClient = await _db.ClientLocations
            .AnyAsync(l => l.Id == request.ClientLocationId && l.ClientId == contract.ClientId, cancellationToken);
        if (!locationBelongsToClient)
        {
            throw new ConflictException("La sede indicada no pertenece al cliente del contrato.");
        }

        var hasActiveLink = await _db.ContractAssets
            .AnyAsync(ca => ca.AssetId == request.AssetId && ca.EndDate == null, cancellationToken);
        if (hasActiveLink)
        {
            throw new ConflictException("Este activo ya está vinculado a un contrato activo.");
        }

        // Mueve el activo a PendienteInstalacion ya asociado a la sede de destino — evita que el
        // coordinador tenga que ir aparte al módulo de Activos a repetir la misma información. Si el
        // activo no está en EnBodega, esto lanza ConflictException solo (la tabla de transiciones de
        // AssetService no permite otro origen), sin necesitar un chequeo aparte acá.
        await _assetService.PrepareStatusChangeAsync(
            request.AssetId,
            new ChangeAssetStatusRequest
            {
                NewStatus = nameof(AssetLifecycleStatus.PendienteInstalacion),
                ClientLocationId = request.ClientLocationId
            },
            changedByUserId,
            cancellationToken);

        var contractAsset = new ContractAsset
        {
            ContractId = contractId,
            AssetId = request.AssetId,
            StartDate = request.StartDate ?? DateTime.UtcNow
        };

        _db.ContractAssets.Add(contractAsset);

        // Un solo SaveChangesAsync: el cambio de estado del activo (+ su AssetStatusLog) y el nuevo
        // ContractAsset se confirman juntos, atómicamente.
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(contractAsset.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<ContractAssetDto>> ListByContractAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        return await Projected(_db)
            .Where(ca => ca.ContractId == contractId)
            .OrderByDescending(ca => ca.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<ContractAssetDto> EndAsync(Guid contractId, Guid id, CancellationToken cancellationToken = default)
    {
        var contractAsset = await _db.ContractAssets
            .FirstOrDefaultAsync(ca => ca.Id == id && ca.ContractId == contractId, cancellationToken)
            ?? throw new NotFoundException(nameof(ContractAsset), id);

        contractAsset.EndDate ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    private async Task<ContractAssetDto> ToDtoAsync(Guid id, CancellationToken cancellationToken) =>
        await Projected(_db).FirstAsync(ca => ca.Id == id, cancellationToken);

    private static IQueryable<ContractAssetDto> Projected(IApplicationDbContext db) =>
        db.ContractAssets.Select(ca => new ContractAssetDto
        {
            Id = ca.Id,
            ContractId = ca.ContractId,
            AssetId = ca.AssetId,
            AssetBrandName = ca.Asset.AssetBrand.Name,
            AssetModel = ca.Asset.Model,
            AssetSerialNumber = ca.Asset.SerialNumber,
            StartDate = ca.StartDate,
            EndDate = ca.EndDate
        });
}
