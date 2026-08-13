using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Toner.Application.Assignment;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Infrastructure.Jobs;

// Job recurrente (Hangfire): evalúa cada MaintenanceSchedule activo contra su umbral (contador o tiempo)
// y genera una MaintenanceOrder cuando corresponde. No reprograma NextDueAt/NextDueCounter — eso ocurre
// al completar la orden (ver MaintenanceOrderService.CompleteAsync), para no adelantar la próxima fecha
// mientras la orden generada sigue pendiente.
public class MaintenanceScheduleEvaluationJob
{
    private readonly IApplicationDbContext _db;
    private readonly IAssignmentEngine _assignmentEngine;
    private readonly ILogger<MaintenanceScheduleEvaluationJob> _logger;

    public MaintenanceScheduleEvaluationJob(
        IApplicationDbContext db,
        IAssignmentEngine assignmentEngine,
        ILogger<MaintenanceScheduleEvaluationJob> logger)
    {
        _db = db;
        _assignmentEngine = assignmentEngine;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var schedules = await _db.MaintenanceSchedules.Where(s => s.IsActive).ToListAsync(cancellationToken);

        var createdOrders = new List<MaintenanceOrder>();

        foreach (var schedule in schedules)
        {
            var hasOpenOrder = await _db.MaintenanceOrders.AnyAsync(
                o => o.MaintenanceScheduleId == schedule.Id
                     && o.Status != MaintenanceOrderStatus.Completada
                     && o.Status != MaintenanceOrderStatus.Cancelada,
                cancellationToken);

            if (hasOpenOrder)
            {
                continue;
            }

            var isDue = schedule.FrequencyType == MaintenanceFrequencyType.PorTiempo
                ? schedule.NextDueAt.HasValue && schedule.NextDueAt <= now
                : await IsCounterDueAsync(schedule, cancellationToken);

            if (!isDue)
            {
                continue;
            }

            var order = new MaintenanceOrder
            {
                MaintenanceScheduleId = schedule.Id,
                AssetId = schedule.AssetId,
                Status = MaintenanceOrderStatus.Pendiente,
                ScheduledDate = now
            };
            _db.MaintenanceOrders.Add(order);
            createdOrders.Add(order);
        }

        if (createdOrders.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);

            // Mismo motor de asignación que ServiceTicket: por cobertura de ciudad + menor carga de trabajo.
            foreach (var order in createdOrders)
            {
                await _assignmentEngine.AssignMaintenanceOrderAsync(order.Id, cancellationToken);
            }
        }

        _logger.LogInformation(
            "Evaluación de cronogramas de mantenimiento: {ScheduleCount} activos revisados, {OrderCount} órdenes generadas.",
            schedules.Count,
            createdOrders.Count);

        return createdOrders.Count;
    }

    private async Task<bool> IsCounterDueAsync(MaintenanceSchedule schedule, CancellationToken cancellationToken)
    {
        if (!schedule.NextDueCounter.HasValue)
        {
            return false;
        }

        var lastReading = await _db.MeterReadings
            .Where(m => m.AssetId == schedule.AssetId)
            .OrderByDescending(m => m.ReadingDate)
            .FirstOrDefaultAsync(cancellationToken);

        return lastReading is not null && lastReading.CounterValue >= schedule.NextDueCounter.Value;
    }
}
