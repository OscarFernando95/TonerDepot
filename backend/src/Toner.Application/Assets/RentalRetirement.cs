namespace Toner.Application.Assets;

// Estado de un equipo frente al límite de impresiones del modelo para seguir en alquiler. Se compara contra el contador total
// del equipo (la última lectura registrada), que es su vida útil real. Solo informa: no bloquea ni cambia ningún estado.
public static class RentalRetirement
{
    public const string Approaching = "Proxima";
    public const string Exceeded = "Superada";

    // Status: null (sin aviso), "Proxima" (faltan WarningPrints o menos) o "Superada" (ya alcanzó o pasó el límite).
    // PrintsUntil: impresiones que faltan (negativo = cuánto se pasó); null si no se puede calcular (modelo sin límite o equipo
    // sin lecturas de contador).
    public static (string? Status, long? PrintsUntil) Evaluate(int? threshold, int warningPrints, long? lastCounter)
    {
        if (threshold is not { } limit || lastCounter is not { } counter)
        {
            return (null, null);
        }

        var remaining = limit - counter;
        if (remaining <= 0)
        {
            return (Exceeded, remaining);
        }

        return (remaining <= warningPrints ? Approaching : null, remaining);
    }
}
