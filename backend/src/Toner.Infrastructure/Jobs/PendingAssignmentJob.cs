using Microsoft.Extensions.Logging;
using Toner.Application.Assignment;
using Toner.Application.Common.Interfaces;

namespace Toner.Infrastructure.Jobs;

// Job recurrente (Hangfire): cada pocos minutos reintenta asignar lo que quedó sin técnico por horario
// laboral, festivo o "fuera de la oficina" (ver PendingAssignmentService).
public class PendingAssignmentJob
{
    private readonly PendingAssignmentService _service;
    private readonly IExceptionLogger _exceptionLogger;
    private readonly ILogger<PendingAssignmentJob> _logger;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public PendingAssignmentJob(
        PendingAssignmentService service,
        IExceptionLogger exceptionLogger,
        ILogger<PendingAssignmentJob> logger,
        ITenantContextAccessor tenantContextAccessor)
    {
        _service = service;
        _exceptionLogger = exceptionLogger;
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Recorre tickets de TODOS los clientes y corre fuera de una request HTTP: scope de staff explícito.
        using var tenantScope = _tenantContextAccessor.Push(() => TenantContext.Staff);

        try
        {
            var (tickets, orders) = await _service.RunAsync(cancellationToken);
            if (tickets + orders > 0)
            {
                _logger.LogInformation(
                    "Reasignación pendiente: {Tickets} ticket(s) y {Orders} orden(es) de mantenimiento asignados.", tickets, orders);
            }
        }
        catch (Exception ex)
        {
            await _exceptionLogger.LogAsync(
                source: $"Jobs.{nameof(PendingAssignmentJob)}",
                exception: ex,
                cancellationToken: CancellationToken.None);

            throw;
        }
    }
}
