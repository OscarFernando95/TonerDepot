namespace Toner.Application.Common.Interfaces;

// Serializa las operaciones que LEEN el saldo de un ítem en una ubicación y luego lo DESCUENTAN (hoy: el
// traspaso, cuya regla "no traspasar más de lo que hay" es la única invariante dura de saldo). Sin esto,
// dos traspasos simultáneos pasan ambos el chequeo y dejan el origen en negativo (AUDIT.md H-17).
//
// Deliberadamente estrecho: Application no recibe "transacciones genéricas" ni conoce Npgsql; solo pide
// exclusividad sobre (ítem, ubicación) mientras corre `operation`.
public interface IStockLock
{
    // `operation` corre dentro de una transacción con el lock tomado, y debe hacer UN solo SaveChanges.
    // Puede re-ejecutarse ante un fallo transitorio: lo que haya añadido al contexto un intento fallido
    // se descarta antes del siguiente. `verifySucceeded` decide, si el commit quedó en duda (se perdió la
    // respuesta), si la operación ya se aplicó, para no duplicarla.
    Task RunExclusiveAsync(
        Guid itemId,
        Guid locationId,
        Func<CancellationToken, Task> operation,
        Func<CancellationToken, Task<bool>> verifySucceeded,
        CancellationToken cancellationToken = default);
}
