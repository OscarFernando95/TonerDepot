using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Toner.Application.Common.Interfaces;

namespace Toner.Infrastructure.Persistence;

// IStockLock sobre Postgres: transacción explícita + pg_advisory_xact_lock por (ítem, ubicación).
//
// - Envuelta en el execution strategy: con EnableRetryOnFailure, EF solo admite transacciones explícitas
//   así, y ante un fallo transitorio re-ejecuta la operación ENTERA en una transacción nueva.
// - El lock se libera solo al terminar la transacción (commit o rollback): no puede quedar huérfano.
// - La clave es un hash de 64 bits: una colisión solo serializa dos claves de más, nunca abre una carrera.
// - Scoped y sobre el MISMO TonerDbContext de la request: corre en la conexión que ya fijó
//   TenantContextInterceptor (mismo rol y contexto RLS); si un corte la cerró, EF la reabre al iniciar la
//   transacción y el interceptor vuelve a fijar el contexto.
public sealed class PostgresStockLock : IStockLock
{
    private readonly TonerDbContext _db;

    public PostgresStockLock(TonerDbContext db)
    {
        _db = db;
    }

    public async Task RunExclusiveAsync(
        Guid itemId,
        Guid locationId,
        Func<CancellationToken, Task> operation,
        Func<CancellationToken, Task<bool>> verifySucceeded,
        CancellationToken cancellationToken = default)
    {
        var key = $"stock:{itemId}:{locationId}";
        var trackedBefore = _db.ChangeTracker.Entries().Select(e => e.Entity).ToHashSet(ReferenceEqualityComparer.Instance);

        await _db.Database.CreateExecutionStrategy().ExecuteAsync(
            state: key,
            operation: async (_, lockKey, token) =>
            {
                // Restos de un intento anterior fallido: sin esto EF los insertaría junto con los nuevos.
                DetachEntriesAddedSince(trackedBefore);

                await using var transaction = await _db.Database.BeginTransactionAsync(token);
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", token);

                await operation(token);

                await transaction.CommitAsync(token);
                return true;
            },
            verifySucceeded: async (_, _, token) => new ExecutionResult<bool>(await verifySucceeded(token), true),
            cancellationToken: cancellationToken);
    }

    private void DetachEntriesAddedSince(HashSet<object> trackedBefore)
    {
        foreach (var entry in _db.ChangeTracker.Entries().Where(e => !trackedBefore.Contains(e.Entity)).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
