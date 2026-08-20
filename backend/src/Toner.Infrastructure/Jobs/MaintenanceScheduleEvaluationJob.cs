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
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public MaintenanceScheduleEvaluationJob(
        IApplicationDbContext db,
        IMaintenanceScheduleEngine engine,
        IAssignmentEngine assignmentEngine,
        IExceptionLogger exceptionLogger,
        ILogger<MaintenanceScheduleEvaluationJob> logger,
        ITenantContextAccessor tenantContextAccessor)
    {
        _db = db;
        _engine = engine;
        _assignmentEngine = assignmentEngine;
        _exceptionLogger = exceptionLogger;
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        // Scope de staff explícito, nunca por omisión: el job recorre los cronogramas de TODOS los
        // clientes, y corre fuera de una request HTTP, así que no hay TenantContextMiddleware que
        // establezca el contexto. Sin esto el TenantContextInterceptor lanzaría (ver hallazgo #5).
        using var tenantScope = _tenantContextAccessor.Push(() => TenantContext.Staff);

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

        // Un puñado de consultas para TODOS los cronogramas activos, en vez de ~3 por cronograma
        // (CODE_QUALITY_AUDIT.md hallazgo #6) — ver MaintenanceScheduleEngine.EvaluateAllDueAsync.
        var createdOrders = await _engine.EvaluateAllDueAsync(now, cancellationToken);

        // Mismo motor de asignación que ServiceTicket: por cobertura de ciudad + menor carga de
        // trabajo. Corre ANTES del único SaveChanges, sobre las órdenes todavía sin guardar: o se
        // persisten todas las órdenes CON su asignación, o no se persiste ninguna.
        //
        // Antes era un SaveChanges para las órdenes y otro por cada asignación, y una caída a mitad
        // del loop dejaba órdenes Pendiente huérfanas de forma PERMANENTE: el reintento de Hangfire
        // no las cura, porque EvaluateAllDueAsync salta los cronogramas que ya tienen orden abierta.
        // Esas impresoras se quedaban sin mantenimiento preventivo en silencio
        // (CODE_QUALITY_AUDIT.md hallazgo #8).
        foreach (var order in createdOrders)
        {
            await _assignmentEngine.AssignMaintenanceOrderAsync(order, cancellationToken);
        }

        if (createdOrders.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Cuántos cronogramas activos se evaluaron, solo para el log — ya no hace falta traerlos aparte
        // para el barrido en sí (EvaluateAllDueAsync ya los cargó internamente).
        var scheduleCount = await _db.MaintenanceSchedules.CountAsync(s => s.IsActive, cancellationToken);

        _logger.LogInformation(
            "Evaluación de cronogramas de mantenimiento: {ScheduleCount} activos revisados, {OrderCount} órdenes generadas.",
            scheduleCount,
            createdOrders.Count);

        return createdOrders.Count;
    }
}
