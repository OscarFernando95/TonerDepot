using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Maintenance;

public class MaintenanceScheduleEngine : IMaintenanceScheduleEngine
{
    // Ventana de anticipación: se genera la orden cuando falta esto o menos, no solo cuando ya venció.
    private const int LeadDays = 15;
    private const int LeadPrints = 5000;

    private readonly IApplicationDbContext _db;

    public MaintenanceScheduleEngine(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<MaintenanceSchedule> UpsertForInstallationAsync(
        Guid assetId,
        Guid contractId,
        long counterValue,
        DateTime counterDate,
        bool generalMaintenanceDone,
        bool unitsMaintenanceDone,
        long? existingConsumablesPrints,
        CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.Include(a => a.AssetModel).FirstAsync(a => a.Id == assetId, cancellationToken);
        var model = asset.AssetModel;

        var schedule = await _db.MaintenanceSchedules.FirstOrDefaultAsync(s => s.AssetId == assetId, cancellationToken);
        if (schedule is null)
        {
            schedule = new MaintenanceSchedule { AssetId = assetId };
            _db.MaintenanceSchedules.Add(schedule);
        }

        schedule.ContractId = contractId;
        schedule.IsActive = true;

        // "Realizado" (true) arranca una línea base fresca (un intervalo completo por delante). No marcado
        // se considera vencido de inmediato, para que la próxima evaluación lo detecte sin más información.
        if (generalMaintenanceDone)
        {
            schedule.LastGeneralMaintenanceAt = counterDate;
            schedule.LastGeneralMaintenanceCounter = counterValue;
            schedule.NextGeneralDueAt = counterDate.AddMonths(model.GeneralMonthsInterval);
            schedule.NextGeneralDueCounter = counterValue + model.GeneralPrintThreshold;
        }
        else
        {
            schedule.LastGeneralMaintenanceAt = null;
            schedule.LastGeneralMaintenanceCounter = null;
            schedule.NextGeneralDueAt = counterDate;
            schedule.NextGeneralDueCounter = counterValue;
        }

        // Instalar/cambiar insumos ya es, por definición, mantenimiento de unidades (los insumos viven
        // dentro de las unidades) — ambas sub-reglas siempre arrancan juntas desde la instalación, sea que
        // los insumos sean nuevos (offset 0) o que ya traigan uso conocido (existingConsumablesPrints).
        schedule.LastUnitsMaintenanceAt = counterDate;
        schedule.LastUnitsMaintenanceCounter = counterValue;
        schedule.NextUnitsDueAt = counterDate.AddMonths(model.UnitsMonthsInterval);
        schedule.NextUnitsDueCounter = counterValue + model.UnitsPrintThreshold;

        var offset = unitsMaintenanceDone
            ? Math.Clamp(existingConsumablesPrints ?? 0, 0, model.ConsumablesPrintThreshold)
            : 0;
        schedule.LastConsumablesChangeAt = counterDate;
        schedule.LastConsumablesChangeCounter = counterValue;
        schedule.NextConsumablesDueCounter = counterValue + (model.ConsumablesPrintThreshold - offset);

        return schedule;
    }

    public async Task<MaintenanceOrder?> EvaluateAsync(
        Guid assetId,
        long currentCounter,
        DateTime asOf,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _db.MaintenanceSchedules
            .Include(s => s.Asset).ThenInclude(a => a.AssetModel)
            .FirstOrDefaultAsync(s => s.AssetId == assetId && s.IsActive, cancellationToken);
        if (schedule is null)
        {
            return null;
        }

        var hasOpenOrder = await _db.MaintenanceOrders.AnyAsync(
            o => o.MaintenanceScheduleId == schedule.Id
                 && o.Status != MaintenanceOrderStatus.Completada
                 && o.Status != MaintenanceOrderStatus.Cancelada,
            cancellationToken);
        if (hasOpenOrder)
        {
            return null;
        }

        var model = schedule.Asset.AssetModel;

        var generalDueSoon = (schedule.NextGeneralDueAt - asOf).TotalDays <= LeadDays
            || (schedule.NextGeneralDueCounter - currentCounter) <= LeadPrints;
        var unitsDueSoon = (schedule.NextUnitsDueAt - asOf).TotalDays <= LeadDays
            || (schedule.NextUnitsDueCounter - currentCounter) <= LeadPrints;
        var consumablesDueSoon = (schedule.NextConsumablesDueCounter - currentCounter) <= LeadPrints;

        var order = new MaintenanceOrder
        {
            MaintenanceScheduleId = schedule.Id,
            AssetId = assetId,
            Status = MaintenanceOrderStatus.Pendiente,
            ScheduledDate = asOf
        };

        if (generalDueSoon)
        {
            // Si el mantenimiento general está por vencer, la orden SIEMPRE se combina con lo que esté
            // más cerca de vencer entre unidades e insumos — aunque esa otra regla no esté aún "por
            // vencer" bajo su propio umbral — para aprovechar la visita técnica. Nunca se genera una
            // orden de "General" sola.
            var pairWithUnits = MaintenanceComboCalculator.GeneralPairsWithUnits(schedule, model, currentCounter, asOf);

            order.IncludesGeneral = true;
            order.IncludesUnits = pairWithUnits;
            order.IncludesConsumables = !pairWithUnits;
        }
        else if (unitsDueSoon)
        {
            order.IncludesUnits = true;
        }
        else if (consumablesDueSoon)
        {
            order.IncludesConsumables = true;
        }
        else
        {
            return null;
        }

        _db.MaintenanceOrders.Add(order);
        return order;
    }

    public async Task RecalculateAfterMaintenanceAsync(
        Guid scheduleId,
        long counterValue,
        DateTime counterDate,
        bool includesGeneral,
        bool includesUnits,
        bool includesConsumables,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _db.MaintenanceSchedules
            .Include(s => s.Asset).ThenInclude(a => a.AssetModel)
            .FirstAsync(s => s.Id == scheduleId, cancellationToken);
        var model = schedule.Asset.AssetModel;

        // Cada sub-regla solo se da por atendida si la orden completada la incluía — si no, su
        // vencimiento se deja tal cual (la visita no la atendió), aunque el contador nuevo ya sirva de
        // referencia para el próximo cálculo de las que sí se avanzan.
        if (includesGeneral)
        {
            schedule.LastGeneralMaintenanceAt = counterDate;
            schedule.LastGeneralMaintenanceCounter = counterValue;
            schedule.NextGeneralDueAt = counterDate.AddMonths(model.GeneralMonthsInterval);
            schedule.NextGeneralDueCounter = counterValue + model.GeneralPrintThreshold;
        }

        // Cambiar insumos ya cuenta como mantenimiento de unidades (viven adentro), así que una orden que
        // incluya insumos también resetea unidades aunque no la hubiera marcado explícitamente.
        if (includesUnits || includesConsumables)
        {
            schedule.LastUnitsMaintenanceAt = counterDate;
            schedule.LastUnitsMaintenanceCounter = counterValue;
            schedule.NextUnitsDueAt = counterDate.AddMonths(model.UnitsMonthsInterval);
            schedule.NextUnitsDueCounter = counterValue + model.UnitsPrintThreshold;
        }

        if (includesConsumables)
        {
            schedule.LastConsumablesChangeAt = counterDate;
            schedule.LastConsumablesChangeCounter = counterValue;
            schedule.NextConsumablesDueCounter = counterValue + model.ConsumablesPrintThreshold;
        }
    }
}
