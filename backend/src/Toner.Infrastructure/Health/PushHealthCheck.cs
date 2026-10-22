using Microsoft.Extensions.Diagnostics.HealthChecks;
using Toner.Infrastructure.Push;

namespace Toner.Infrastructure.Health;

// Cubre lo que el arranque no puede: que alguien que monitorea /health/ready vea el push apagado. Degraded y no
// Unhealthy: la API sirve igual sin push y sacarla de rotación no lo arreglaría. Sin llamadas de red (solo lee el
// estado del transporte), así que no necesita caché.
public class PushHealthCheck : IHealthCheck
{
    private readonly IPushTransport _transport;

    public PushHealthCheck(IPushTransport transport) => _transport = transport;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(_transport.IsEnabled
            ? HealthCheckResult.Healthy()
            // Descripción genérica a propósito: el cuerpo de la respuesta es público.
            : new HealthCheckResult(context.Registration.FailureStatus, "Las notificaciones push están apagadas."));
}
