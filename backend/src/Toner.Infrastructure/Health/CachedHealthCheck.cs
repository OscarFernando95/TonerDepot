using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Toner.Infrastructure.Health;

// /health/ready es anónimo y lo sondea un orquestador cada pocos segundos: sin esto, cada sondeo abriría
// una conexión SMTP o una petición a Blob Storage, y quien conozca la URL podría usarla para martillar
// esas dependencias. Se recuerda el último resultado un rato corto; el check debe registrarse como
// singleton para que la caché sobreviva entre sondeos.
public abstract class CachedHealthCheck : IHealthCheck
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(10);

    private readonly TimeSpan _ttl;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HealthCheckResult _last;
    private DateTimeOffset _lastAt = DateTimeOffset.MinValue;

    protected CachedHealthCheck(TimeSpan? ttl = null, TimeProvider? time = null)
    {
        _ttl = ttl ?? DefaultTtl;
        _time = time ?? TimeProvider.System;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = _time.GetUtcNow();
            if (now - _lastAt < _ttl)
            {
                return _last;
            }

            _last = await CheckCoreAsync(context, cancellationToken);
            _lastAt = _time.GetUtcNow();
            return _last;
        }
        finally
        {
            _gate.Release();
        }
    }

    protected abstract Task<HealthCheckResult> CheckCoreAsync(HealthCheckContext context, CancellationToken cancellationToken);
}
