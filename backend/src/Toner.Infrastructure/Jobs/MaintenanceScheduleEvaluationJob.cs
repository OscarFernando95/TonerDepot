using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Toner.Application.Assignment;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance;

namespace Toner.Infrastructure.Jobs;

// Job recurrente (Hangfire): red de seguridad diaria para el disparador puramente por tiempo del
// mantenimiento general (que no depende de que llegue una lectura de contador nueva). El disparador por
// contador ya se evalúa en tiempo real cada vez que se registra una lectura (ver
// MaintenanceScheduleEngine y sus llamadores: AssetService.AddMeterReadingAsync,
// TechnicianCheckInService.CheckOutAsync, el módulo de Lectura de contadores).
public class MaintenanceScheduleEvaluationJob
{
    private readonly IApplicationDbContext _db;
    private readonly IMaintenanceScheduleEngine _engine;
    private readonly IAssignmentEngine _assignmentEngine;
    private readonly IExceptionLogger _exceptionLogger;
    private readonly ILogger<MaintenanceScheduleEvaluationJob> _logger;

    public MaintenanceScheduleEvaluationJob(
        IApplicationDbContext db,
        IMaintenanceScheduleEngine engine,
        IAssignmentEngine assignmentEngine,
        IExceptionLogger exceptionLogger,
        ILogger<MaintenanceScheduleEvaluationJob> logger)
    {
        _db = db;
        _engine = engine;
        _assignmentEngine = assignmentEngine;
        _exceptionLogger = exceptionLogger;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await EvaluateSchedulesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // El dashboard de Hangfire (única otra fuente de verdad sobre un fallo de este job) no
            // se expone fuera de Development, así que sin esto un fallo en producción es invisible
            // hasta que alguien nota que no se generaron órdenes. CancellationToken.None: el log debe
            // completarse aunque el motivo del fallo sea que cancellationToken ya se canceló.
            await _exceptionLogger.LogAsync(
                source: $"Jobs.{nameof(MaintenanceScheduleEvaluationJob)}",
                exception: ex,
                cancellationToken: CancellationToken.None);

            throw; // Hangfire debe seguir viendo la excepción para aplicar su política de reintentos.
        }
    }

    private async Task<int> EvaluateSchedulesAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var schedules = await _db.MaintenanceSchedules
            .Where(s => s.IsActive)
            .Select(s => s.AssetId)
            .ToListAsync(cancellationToken);

        var createdOrderIds = new List<Guid>();

        foreach (var assetId in schedules)
        {
            var lastReading = await _db.MeterReadings
                .Where(m => m.AssetId == assetId)
                .OrderByDescending(m => m.ReadingDate)
                .Select(m => (long?)m.CounterValue)
                .FirstOrDefaultAsync(cancellationToken);

            var order = await _engine.EvaluateAsync(assetId, lastReading ?? 0, now, cancellationToken);
            if (order is not null)
            {
                await _db.SaveChangesAsync(cancellationToken);
                createdOrderIds.Add(order.Id);
            }
        }

        // Mismo motor de asignación que ServiceTicket: por cobertura de ciudad + menor carga de trabajo.
        foreach (var orderId in createdOrderIds)
        {
            await _assignmentEngine.AssignMaintenanceOrderAsync(orderId, cancellationToken);
        }

        _logger.LogInformation(
            "Evaluación de cronogramas de mantenimiento: {ScheduleCount} activos revisados, {OrderCount} órdenes generadas.",
            schedules.Count,
            createdOrderIds.Count);

        return createdOrderIds.Count;
    }
}
