using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Contracts.Dtos;
using Toner.Domain.Entities;

namespace Toner.Application.Contracts;

public class ContractAssetService : IContractAssetService
{
    private readonly IApplicationDbContext _db;

    public ContractAssetService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ContractAssetDto> AddAsync(Guid contractId, AddContractAssetRequest request, CancellationToken cancellationToken = default)
    {
        var contractExists = await _db.Contracts.AnyAsync(c => c.Id == contractId, cancellationToken);
        if (!contractExists)
        {
            throw new NotFoundException(nameof(Contract), contractId);
        }

        var hasActiveLink = await _db.ContractAssets
            .AnyAsync(ca => ca.AssetId == request.AssetId && ca.EndDate == null, cancellationToken);
        if (hasActiveLink)
        {
            throw new ConflictException("Este activo ya está vinculado a un contrato activo.");
        }

        var contractAsset = new ContractAsset
        {
            ContractId = contractId,
            AssetId = request.AssetId,
            StartDate = request.StartDate ?? DateTime.UtcNow
        };

        _db.ContractAssets.Add(contractAsset);
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
