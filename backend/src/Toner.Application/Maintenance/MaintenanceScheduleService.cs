using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance.Dtos;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Maintenance;

public class MaintenanceScheduleService : IMaintenanceScheduleService
{
    private readonly IApplicationDbContext _db;
    private readonly IMaintenanceScheduleEngine _engine;

    public MaintenanceScheduleService(IApplicationDbContext db, IMaintenanceScheduleEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    public async Task<IReadOnlyList<MaintenanceScheduleDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await Projected(_db).OrderBy(s => s.CityName).ThenBy(s => s.ClientName).ToListAsync(cancellationToken);
        await AttachLastKnownCountersAsync(items, cancellationToken);
        await AttachMaintenanceSummariesAsync(items, cancellationToken);
        return items;
    }

    public async Task<MaintenanceScheduleDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dto = await Projected(_db).FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(MaintenanceSchedule), id);
        await AttachLastKnownCountersAsync(new[] { dto }, cancellationToken);
        await AttachMaintenanceSummariesAsync(new[] { dto }, cancellationToken);
        return dto;
    }

    public async Task<MaintenanceScheduleDto> SetActiveStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var schedule = await _db.MaintenanceSchedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(MaintenanceSchedule), id);

        schedule.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<MaintenanceScheduleDto>> ListInCoverageAsync(Guid technicianId, CancellationToken cancellationToken = default)
    {
        var coveredCityIds = await _db.TechnicianCoverages
            .Where(c => c.TechnicianId == technicianId)
            .Select(c => c.CityId)
            .ToListAsync(cancellationToken);

        if (coveredCityIds.Count == 0)
        {
            return Array.Empty<MaintenanceScheduleDto>();
        }

        var query = _db.MaintenanceSchedules.Where(s =>
            s.Asset.CurrentClientLocation != null && coveredCityIds.Contains(s.Asset.CurrentClientLocation.CityId));

        var items = await ProjectedFrom(query).OrderBy(s => s.CityName).ThenBy(s => s.ClientName).ToListAsync(cancellationToken);
        await AttachLastKnownCountersAsync(items, cancellationToken);
        await AttachMaintenanceSummariesAsync(items, cancellationToken);
        return items;
    }

    public async Task<int> BackfillMissingAsync(CancellationToken cancellationToken = default)
    {
        var missing = await _db.Assets
            .Where(a => a.LifecycleStatus == AssetLifecycleStatus.Instalado
                && !_db.MaintenanceSchedules.Any(s => s.AssetId == a.Id)
                && _db.ContractAssets.Any(ca => ca.AssetId == a.Id && ca.EndDate == null))
            .Select(a => new
            {
                a.Id,
                ContractId = _db.ContractAssets.Where(ca => ca.AssetId == a.Id && ca.EndDate == null).Select(ca => ca.ContractId).First()
            })
            .ToListAsync(cancellationToken);

        foreach (var asset in missing)
        {
            var lastReading = await _db.MeterReadings
                .Where(m => m.AssetId == asset.Id)
                .OrderByDescending(m => m.ReadingDate)
                .FirstOrDefaultAsync(cancellationToken);

            // Conservador: si no hay lectura, arranca en 0 desde ahora. Sin dato histórico real (backfill
            // de activos instalados antes de este rediseño), se asume que todo — general, unidades e
            // insumos — está al día a partir de este punto (unitsMaintenanceDone=false ya implica insumos
            // "nuevos", y por lo tanto unidades también arrancan frescas junto con ellos).
            await _engine.UpsertForInstallationAsync(
                asset.Id,
                asset.ContractId,
                lastReading?.CounterValue ?? 0,
                lastReading?.ReadingDate ?? DateTime.UtcNow,
                generalMaintenanceDone: true,
                unitsMaintenanceDone: false,
                existingConsumablesPrints: null,
                cancellationToken);
        }

        if (missing.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return missing.Count;
    }

    private async Task AttachLastKnownCountersAsync(IReadOnlyList<MaintenanceScheduleDto> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var assetIds = items.Select(i => i.AssetId).ToList();
        var readings = await _db.MeterReadings
            .Where(m => assetIds.Contains(m.AssetId))
            .Select(m => new { m.AssetId, m.ReadingDate, m.CounterValue })
            .ToListAsync(cancellationToken);

        var lastByAsset = readings
            .GroupBy(r => r.AssetId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ReadingDate).First().CounterValue);

        foreach (var item in items)
        {
            item.LastKnownCounter = lastByAsset.TryGetValue(item.AssetId, out var value) ? value : null;
        }
    }

    // "Último mantenimiento" = el más reciente entre las 3 sub-reglas, con las glosas (MG/MU/CI) de las
    // que comparten esa misma fecha. "Próximo mantenimiento" viene de MaintenanceComboCalculator — la
    // misma matemática de proximidad que usa MaintenanceScheduleEngine.EvaluateAsync para elegir el
    // acompañante de una orden real, pero sin esperar a la ventana de anticipación. Requiere las
    // entidades (no solo el DTO) porque necesita los umbrales de AssetModel.
    private async Task AttachMaintenanceSummariesAsync(IReadOnlyList<MaintenanceScheduleDto> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var scheduleIds = items.Select(i => i.Id).ToList();
        var schedules = await _db.MaintenanceSchedules
            .Include(s => s.Asset).ThenInclude(a => a.AssetModel)
            .Where(s => scheduleIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
        var scheduleById = schedules.ToDictionary(s => s.Id);

        var asOf = DateTime.UtcNow;
        foreach (var item in items)
        {
            if (!scheduleById.TryGetValue(item.Id, out var schedule))
            {
                continue;
            }

            var combo = MaintenanceComboCalculator.DetermineLeadingCombo(
                schedule, schedule.Asset.AssetModel, item.LastKnownCounter ?? 0, asOf);
            item.NextMaintenanceAt = combo.At;
            item.NextMaintenanceCounter = combo.Counter;
            item.NextMaintenanceCodes = ComboCodes(combo.IncludesGeneral, combo.IncludesUnits, combo.IncludesConsumables);

            var lastCandidates = new (DateTime? At, string Code)[]
            {
                (item.LastGeneralMaintenanceAt, "MG"),
                (item.LastUnitsMaintenanceAt, "MU"),
                (item.LastConsumablesChangeAt, "CI")
            }.Where(c => c.At.HasValue).ToList();

            if (lastCandidates.Count > 0)
            {
                var maxAt = lastCandidates.Max(c => c.At!.Value);
                item.LastMaintenanceAt = maxAt;
                item.LastMaintenanceCodes = lastCandidates.Where(c => c.At!.Value == maxAt).Select(c => c.Code).ToList();
            }
        }
    }

    private static List<string> ComboCodes(bool includesGeneral, bool includesUnits, bool includesConsumables)
    {
        var codes = new List<string>();
        if (includesGeneral) codes.Add("MG");
        if (includesUnits) codes.Add("MU");
        if (includesConsumables) codes.Add("CI");
        return codes;
    }

    private static IQueryable<MaintenanceScheduleDto> Projected(IApplicationDbContext db) => ProjectedFrom(db.MaintenanceSchedules);

    private static IQueryable<MaintenanceScheduleDto> ProjectedFrom(IQueryable<MaintenanceSchedule> query) =>
        query.Select(s => new MaintenanceScheduleDto
        {
            Id = s.Id,
            AssetId = s.AssetId,
            AssetBrandName = s.Asset.AssetModel.AssetBrand.Name,
            AssetModel = s.Asset.AssetModel.Name,
            AssetSerialNumber = s.Asset.SerialNumber,
            ContractId = s.ContractId,
            ClientId = s.Contract.ClientId,
            ClientName = s.Contract.Client.Name,
            ClientLocationName = s.Asset.CurrentClientLocation != null ? s.Asset.CurrentClientLocation.Name : null,
            CityName = s.Asset.CurrentClientLocation != null ? s.Asset.CurrentClientLocation.City.Name : null,
            Area = s.Asset.Area,
            IsActive = s.IsActive,
            LastGeneralMaintenanceAt = s.LastGeneralMaintenanceAt,
            LastGeneralMaintenanceCounter = s.LastGeneralMaintenanceCounter,
            NextGeneralDueAt = s.NextGeneralDueAt,
            NextGeneralDueCounter = s.NextGeneralDueCounter,
            LastUnitsMaintenanceAt = s.LastUnitsMaintenanceAt,
            LastUnitsMaintenanceCounter = s.LastUnitsMaintenanceCounter,
            NextUnitsDueAt = s.NextUnitsDueAt,
            NextUnitsDueCounter = s.NextUnitsDueCounter,
            LastConsumablesChangeAt = s.LastConsumablesChangeAt,
            LastConsumablesChangeCounter = s.LastConsumablesChangeCounter,
            NextConsumablesDueCounter = s.NextConsumablesDueCounter,
            CreatedAt = s.CreatedAt
        });
}
