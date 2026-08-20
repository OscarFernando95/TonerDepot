using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Paging;
using Toner.Application.Assets;
using Toner.Application.Assets.Dtos;
using Toner.Application.Common;
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

    public async Task<PagedResult<ContractAssetDto>> ListByContractAsync(Guid contractId, int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        var result = await Projected(_db)
            .Where(ca => ca.ContractId == contractId)
            .OrderByDescending(ca => ca.StartDate)
            .ToOffsetPageAsync(page, pageSize, cancellationToken);

        await AttachAssetMetricsAsync(result.Items, cancellationToken);

        return result;
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

    private async Task<ContractAssetDto> ToDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        var dto = await Projected(_db).FirstAsync(ca => ca.Id == id, cancellationToken);
        await AttachAssetMetricsAsync(new[] { dto }, cancellationToken);
        return dto;
    }

    // El promedio de impresiones por mes no tiene un criterio previo en el sistema: se define acá
    // como (última lectura - primera lectura) / meses transcurridos entre esas dos fechas — solo si
    // hay 2+ lecturas que no sean del mismo instante (elapsedDays > 0 ya cubre el caso de una sola
    // lectura, donde primera y última son la misma fila); si no, se deja null y el frontend
    // simplemente no muestra la celda.
    private async Task AttachAssetMetricsAsync(IReadOnlyList<ContractAssetDto> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var assetIds = items.Select(i => i.AssetId).Distinct().ToList();

        var areasByAsset = await _db.Assets
            .Where(a => assetIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Area })
            .ToDictionaryAsync(a => a.Id, a => a.Area, cancellationToken);

        var (firstByAsset, lastByAsset) = await MeterReadingQueries.GetFirstAndLastReadingsByAssetAsync(_db, assetIds, cancellationToken);

        foreach (var item in items)
        {
            item.Area = areasByAsset.GetValueOrDefault(item.AssetId);

            if (!lastByAsset.TryGetValue(item.AssetId, out var last))
            {
                continue;
            }

            item.LastMeterReading = last.CounterValue;

            if (!firstByAsset.TryGetValue(item.AssetId, out var first))
            {
                continue;
            }

            var elapsedDays = (last.ReadingDate - first.ReadingDate).TotalDays;
            if (elapsedDays <= 0)
            {
                continue;
            }

            var monthsElapsed = elapsedDays / 30.44;
            item.AverageMonthlyPrints = Math.Round((last.CounterValue - first.CounterValue) / monthsElapsed, 1);
        }
    }

    private static IQueryable<ContractAssetDto> Projected(IApplicationDbContext db) =>
        db.ContractAssets.Select(ca => new ContractAssetDto
        {
            Id = ca.Id,
            ContractId = ca.ContractId,
            AssetId = ca.AssetId,
            AssetBrandName = ca.Asset.AssetModel.AssetBrand.Name,
            AssetModel = ca.Asset.AssetModel.Name,
            AssetSerialNumber = ca.Asset.SerialNumber,
            StartDate = ca.StartDate,
            EndDate = ca.EndDate
        });
}
