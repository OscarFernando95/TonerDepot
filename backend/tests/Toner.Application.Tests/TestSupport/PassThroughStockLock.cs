using Toner.Application.Common.Interfaces;

namespace Toner.Application.Tests.TestSupport;

// Para tests con InMemory, que no soporta transacciones ni SQL crudo: ejecuta la operación sin bloquear.
// El bloqueo real (PostgresStockLock) se prueba contra Postgres (InventoryConcurrencyPostgresTests,
// StockNeverNegativePostgresTests). Las REGLAS de saldo sí se prueban aquí: no dependen del lock.
public sealed class PassThroughStockLock : IStockLock
{
    public Task RunInStockTransactionAsync(
        Func<CancellationToken, Task> operation, Func<CancellationToken, Task<bool>> verifySucceeded, CancellationToken cancellationToken = default) =>
        operation(cancellationToken);

    public Task LockAsync(IEnumerable<(Guid ItemId, Guid LocationId)> keys, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
