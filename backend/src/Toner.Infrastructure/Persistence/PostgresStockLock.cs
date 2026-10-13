using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Toner.Application.Common.Interfaces;

namespace Toner.Infrastructure.Persistence;

// IStockLock sobre Postgres: transacción explícita + pg_advisory_xact_lock por (ítem, ubicación).
//
// - Envuelta en el execution strategy: con EnableRetryOnFailure, EF solo admite transacciones explícitas
//   así, y ante un fallo transitorio re-ejecuta la operación ENTERA en una transacción nueva.
// - Los locks se liberan solos al terminar la transacción (commit o rollback): no pueden quedar huérfanos.
// - Clave = hash de 64 bits: una colisión solo serializa dos claves de más, nunca abre una carrera.
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

    public async Task RunInStockTransactionAsync(
        Func<CancellationToken, Task> operation,
        Func<CancellationToken, Task<bool>> verifySucceeded,
        CancellationToken cancellationToken = default)
    {
        if (_db.Database.CurrentTransaction is not null)
        {
            // Ya dentro de una transacción de stock: se une a ella (sus locks y su commit).
            await operation(cancellationToken);
            return;
        }

        var trackedBefore = _db.ChangeTracker.Entries().Select(e => e.Entity).ToHashSet(ReferenceEqualityComparer.Instance);

        await _db.Database.CreateExecutionStrategy().ExecuteAsync(
            state: 0,
            operation: async (_, _, token) =>
            {
                // Restos de un intento anterior fallido: sin esto EF los insertaría junto con los nuevos.
                DetachEntriesAddedSince(trackedBefore);

                await using var transaction = await _db.Database.BeginTransactionAsync(token);
                await operation(token);
                await transaction.CommitAsync(token);
                return true;
            },
            verifySucceeded: async (_, _, token) => new ExecutionResult<bool>(await verifySucceeded(token), true),
            cancellationToken: cancellationToken);
    }

    public async Task LockAsync(IEnumerable<(Guid ItemId, Guid LocationId)> keys, CancellationToken cancellationToken = default)
    {
        if (_db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "IStockLock.LockAsync solo puede usarse dentro de RunInStockTransactionAsync: fuera de una transacción el lock se liberaría al instante.");
        }

        // Orden estable: dos operaciones con claves solapadas las toman en el mismo orden y no se bloquean en cruz.
        foreach (var key in keys.Select(k => $"stock:{k.ItemId}:{k.LocationId}").Distinct().OrderBy(k => k, StringComparer.Ordinal))
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
        }
    }

    private void DetachEntriesAddedSince(HashSet<object> trackedBefore)
    {
        foreach (var entry in _db.ChangeTracker.Entries().Where(e => !trackedBefore.Contains(e.Entity)).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
