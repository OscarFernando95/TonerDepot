using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Toner.Infrastructure.Persistence;

namespace Toner.Infrastructure.Health;

// Usa la factory (no el TonerDbContext scoped normal) para no competir por la misma conexión que el
// resto de la request y para poder correr fuera de un scope de request si algún orquestador llama a
// /health antes de que el resto del pipeline esté listo. Pasa por TenantContextInterceptor igual que
// cualquier otra conexión — CanConnectAsync abre una conexión real — pero TenantContextMiddleware ya
// estableció el contexto (Anonymous, si la request no está autenticada) antes de que el endpoint se
// ejecute, así que el interceptor no lanza.
public class PostgresHealthCheck : IHealthCheck
{
    private readonly IDbContextFactory<TonerDbContext> _dbContextFactory;

    public PostgresHealthCheck(IDbContextFactory<TonerDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("No se pudo conectar a la base de datos.");
    }
}
