using Microsoft.Extensions.Logging;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance;

namespace Toner.Infrastructure.Jobs;

// Job recurrente (Hangfire): cada rato cancela las órdenes que un técnico inició por su cuenta y no empezó en el plazo
// configurado (ver AbandonedSelfStartedOrderService).
public class AbandonedSelfStartedOrderJob
{
    private readonly AbandonedSelfStartedOrderService _service;
    private readonly IExceptionLogger _exceptionLogger;
    private readonly ILogger<AbandonedSelfStartedOrderJob> _logger;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public AbandonedSelfStartedOrderJob(
        AbandonedSelfStartedOrderService service,
        IExceptionLogger exceptionLogger,
        ILogger<AbandonedSelfStartedOrderJob> logger,
        ITenantContextAccessor tenantContextAccessor)
    {
        _service = service;
        _exceptionLogger = exceptionLogger;
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Recorre órdenes de TODOS los clientes y corre fuera de una request HTTP: scope de staff explícito.
        using var tenantScope = _tenantContextAccessor.Push(() => TenantContext.Staff);

        try
        {
            var cancelled = await _service.RunAsync(cancellationToken);
            if (cancelled > 0)
            {
                _logger.LogInformation("Órdenes autoiniciadas caducadas: {Count} cancelada(s).", cancelled);
            }
        }
        catch (Exception ex)
        {
            await _exceptionLogger.LogAsync(
                source: $"Jobs.{nameof(AbandonedSelfStartedOrderJob)}",
                exception: ex,
                cancellationToken: CancellationToken.None);

            throw;
        }
    }
}
