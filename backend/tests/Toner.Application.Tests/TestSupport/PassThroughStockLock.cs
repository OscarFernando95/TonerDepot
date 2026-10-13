using Toner.Application.Common.Interfaces;

namespace Toner.Application.Tests.TestSupport;

// Para tests con InMemory, que no soporta transacciones ni SQL crudo: ejecuta la operación sin bloquear.
// El bloqueo real (PostgresStockLock) se prueba contra Postgres en InventoryConcurrencyPostgresTests.
public sealed class PassThroughStockLock : IStockLock
{
    public Task RunExclusiveAsync(
        Guid itemId, Guid locationId, Func<CancellationToken, Task> operation,
        Func<CancellationToken, Task<bool>> verifySucceeded, CancellationToken cancellationToken = default) =>
        operation(cancellationToken);
}
