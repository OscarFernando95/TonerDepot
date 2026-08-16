using Microsoft.EntityFrameworkCore;
using Toner.Application.Assets.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;

namespace Toner.Application.Assets;

public class AssetModelService : IAssetModelService
{
    private readonly IApplicationDbContext _db;

    public AssetModelService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AssetModelDto>> ListAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        return await _db.AssetModels
            .Where(m => m.AssetBrandId == brandId)
            .OrderBy(m => m.Name)
            .Select(m => new AssetModelDto
            {
                Id = m.Id,
                AssetBrandId = m.AssetBrandId,
                Name = m.Name,
                GeneralPrintThreshold = m.GeneralPrintThreshold,
                GeneralMonthsInterval = m.GeneralMonthsInterval,
                UnitsPrintThreshold = m.UnitsPrintThreshold,
                UnitsMonthsInterval = m.UnitsMonthsInterval,
                ConsumablesPrintThreshold = m.ConsumablesPrintThreshold
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<AssetModelDto> CreateAsync(Guid brandId, CreateAssetModelRequest request, CancellationToken cancellationToken = default)
    {
        var brandExists = await _db.AssetBrands.AnyAsync(b => b.Id == brandId, cancellationToken);
        if (!brandExists)
        {
            throw new NotFoundException(nameof(AssetBrand), brandId);
        }

        var name = request.Name.Trim();

        var exists = await _db.AssetModels.AnyAsync(m => m.AssetBrandId == brandId && m.Name == name, cancellationToken);
        if (exists)
        {
            throw new ConflictException($"Ya existe un modelo llamado '{name}' para esta marca.");
        }

        var model = new AssetModel
        {
            AssetBrandId = brandId,
            Name = name,
            GeneralPrintThreshold = request.GeneralPrintThreshold,
            GeneralMonthsInterval = request.GeneralMonthsInterval,
            UnitsPrintThreshold = request.UnitsPrintThreshold,
            UnitsMonthsInterval = request.UnitsMonthsInterval,
            ConsumablesPrintThreshold = request.ConsumablesPrintThreshold
        };
        _db.AssetModels.Add(model);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(model);
    }

    public async Task<AssetModelDto> UpdateAsync(Guid brandId, Guid id, UpdateAssetModelRequest request, CancellationToken cancellationToken = default)
    {
        var model = await _db.AssetModels.FirstOrDefaultAsync(m => m.Id == id && m.AssetBrandId == brandId, cancellationToken)
            ?? throw new NotFoundException(nameof(AssetModel), id);

        var name = request.Name.Trim();

        var nameTaken = await _db.AssetModels.AnyAsync(
            m => m.AssetBrandId == brandId && m.Id != id && m.Name == name, cancellationToken);
        if (nameTaken)
        {
            throw new ConflictException($"Ya existe un modelo llamado '{name}' para esta marca.");
        }

        model.Name = name;
        model.GeneralPrintThreshold = request.GeneralPrintThreshold;
        model.GeneralMonthsInterval = request.GeneralMonthsInterval;
        model.UnitsPrintThreshold = request.UnitsPrintThreshold;
        model.UnitsMonthsInterval = request.UnitsMonthsInterval;
        model.ConsumablesPrintThreshold = request.ConsumablesPrintThreshold;

        await RecalculateSchedulesForModelAsync(model, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(model);
    }

    // Los umbrales quedan "materializados" en cada MaintenanceSchedule (NextXDueCounter/NextXDueAt) al
    // calcularse — editar el modelo después no los toca solo. Cuando Staff ajusta un umbral, todo
    // cronograma de un activo de ese modelo que ya tenga una línea base real (LastX no nulo) se
    // recalcula desde esa misma línea base con el umbral nuevo. Las sub-reglas "vencidas de inmediato"
    // (LastX nulo) no dependen de ningún umbral, así que quedan igual.
    private async Task RecalculateSchedulesForModelAsync(AssetModel model, CancellationToken cancellationToken)
    {
        var schedules = await _db.MaintenanceSchedules
            .Where(s => s.Asset.AssetModelId == model.Id)
            .ToListAsync(cancellationToken);

        foreach (var schedule in schedules)
        {
            if (schedule.LastGeneralMaintenanceCounter.HasValue)
            {
                schedule.NextGeneralDueCounter = schedule.LastGeneralMaintenanceCounter.Value + model.GeneralPrintThreshold;
            }
            if (schedule.LastGeneralMaintenanceAt.HasValue)
            {
                schedule.NextGeneralDueAt = schedule.LastGeneralMaintenanceAt.Value.AddMonths(model.GeneralMonthsInterval);
            }
            if (schedule.LastUnitsMaintenanceCounter.HasValue)
            {
                schedule.NextUnitsDueCounter = schedule.LastUnitsMaintenanceCounter.Value + model.UnitsPrintThreshold;
            }
            if (schedule.LastUnitsMaintenanceAt.HasValue)
            {
                schedule.NextUnitsDueAt = schedule.LastUnitsMaintenanceAt.Value.AddMonths(model.UnitsMonthsInterval);
            }
            if (schedule.LastConsumablesChangeCounter.HasValue)
            {
                schedule.NextConsumablesDueCounter = schedule.LastConsumablesChangeCounter.Value + model.ConsumablesPrintThreshold;
            }
        }
    }

    private static AssetModelDto ToDto(AssetModel m) => new()
    {
        Id = m.Id,
        AssetBrandId = m.AssetBrandId,
        Name = m.Name,
        GeneralPrintThreshold = m.GeneralPrintThreshold,
        GeneralMonthsInterval = m.GeneralMonthsInterval,
        UnitsPrintThreshold = m.UnitsPrintThreshold,
        UnitsMonthsInterval = m.UnitsMonthsInterval,
        ConsumablesPrintThreshold = m.ConsumablesPrintThreshold
    };
}
