namespace Toner.Application.Common.Interfaces;

// Garantiza que el saldo de inventario NUNCA quede en negativo cuando varias operaciones leen y descuentan el
// mismo (ítem, ubicación) a la vez: traspasos (AUDIT.md H-17), consumo en visitas, tóner y ajustes (H-41).
//
// Deliberadamente estrecho: Application no recibe "transacciones genéricas" ni conoce Npgsql.
public interface IStockLock
{
    // Corre `operation` en una transacción; `operation` debe hacer UN solo SaveChanges. Puede re-ejecutarse ante un
    // fallo transitorio (lo que añadió al contexto un intento fallido se descarta antes del siguiente), así que
    // debe leer dentro lo que necesite. `verifySucceeded` decide, si el commit quedó en duda, si ya se aplicó.
    // Si ya hay una transacción en curso, `operation` se une a ella.
    Task RunInStockTransactionAsync(
        Func<CancellationToken, Task> operation,
        Func<CancellationToken, Task<bool>> verifySucceeded,
        CancellationToken cancellationToken = default);

    // Solo dentro de RunInStockTransactionAsync: toma, en orden estable (sin riesgo de deadlock entre operaciones
    // con varias claves), la exclusividad de cada (ítem, ubicación) hasta el final de la transacción. Llamarlo
    // ANTES de leer los saldos que se van a comprobar.
    Task LockAsync(IEnumerable<(Guid ItemId, Guid LocationId)> keys, CancellationToken cancellationToken = default);
}
