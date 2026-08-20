using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Toner.Application.Tests.TestSupport;

// Cuenta los SaveChangesAsync de una operación y, opcionalmente, hace fallar uno concreto.
//
// Sirve para fijar la propiedad del hallazgo #8: cada operación de negocio debe terminar con
// EXACTAMENTE un SaveChanges. La forma de probarlo sin depender de detalles internos es hacer fallar
// el SEGUNDO: si el código está bien, ese segundo nunca ocurre y la operación termina completa; si
// alguien reintroduce un commit intermedio, el fallo simulado se dispara y el test se pone rojo
// mostrando exactamente el estado a medias que el hallazgo describía.
//
// Hacer fallar el PRIMERO no distinguiría nada: tanto el código correcto como el roto escribirían
// cero filas en ese caso.
public sealed class SaveChangesSpyInterceptor : SaveChangesInterceptor
{
    public int SaveCount { get; private set; }

    // 1 = falla el primero, 2 = falla el segundo, null = nunca falla (solo cuenta).
    public int? FailOnSaveNumber { get; init; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SaveCount++;

        if (FailOnSaveNumber == SaveCount)
        {
            throw new SimulatedSaveFailureException(SaveCount);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        SaveCount++;

        if (FailOnSaveNumber == SaveCount)
        {
            throw new SimulatedSaveFailureException(SaveCount);
        }

        return base.SavingChanges(eventData, result);
    }
}

public sealed class SimulatedSaveFailureException : Exception
{
    public SimulatedSaveFailureException(int saveNumber)
        : base($"Fallo simulado en el SaveChanges #{saveNumber}. Si ves esto, la operación hizo más " +
               $"de un commit y volvió a ser vulnerable al estado a medias del hallazgo #8.")
    {
    }
}
