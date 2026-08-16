using Toner.Domain.Entities;

namespace Toner.Application.Maintenance;

// Matemática de proximidad compartida entre MaintenanceScheduleEngine.EvaluateAsync (que decide si YA
// genera una orden real, dentro de una ventana de anticipación) y MaintenanceScheduleService (que
// predice "qué sigue" siempre, para mostrarlo en pantalla, sin esperar a esa ventana).
public static class MaintenanceComboCalculator
{
    public readonly record struct ComboResult(
        bool IncludesGeneral,
        bool IncludesUnits,
        bool IncludesConsumables,
        // Insumos es puro contador (sin componente de tiempo) — si termina siendo la regla líder sin
        // acompañar a general, no hay una fecha estimada real que mostrar.
        DateTime? At,
        long Counter);

    // Proximidad = fracción del umbral ya consumida (0 = recién reseteado, ~1 = justo en el punto de
    // vencimiento, >1 = vencido) — permite comparar "días restantes" vs "impresiones restantes" en una
    // misma escala para decidir cuál sub-regla está más cerca.
    private static double GeneralProximity(MaintenanceSchedule s, AssetModel m, long counter, DateTime asOf) =>
        Math.Min(
            (s.NextGeneralDueAt - asOf).TotalDays / Math.Max(1, m.GeneralMonthsInterval * 30.0),
            (s.NextGeneralDueCounter - counter) / (double)Math.Max(1, m.GeneralPrintThreshold));

    private static double UnitsProximity(MaintenanceSchedule s, AssetModel m, long counter, DateTime asOf) =>
        Math.Min(
            (s.NextUnitsDueAt - asOf).TotalDays / Math.Max(1, m.UnitsMonthsInterval * 30.0),
            (s.NextUnitsDueCounter - counter) / (double)Math.Max(1, m.UnitsPrintThreshold));

    private static double ConsumablesProximity(MaintenanceSchedule s, AssetModel m, long counter) =>
        (s.NextConsumablesDueCounter - counter) / (double)Math.Max(1, m.ConsumablesPrintThreshold);

    // Con cuál de unidades/insumos se debe combinar el mantenimiento general cuando este está por vencer:
    // siempre el más próximo de los dos, aunque ese otro todavía no esté "por vencer" bajo su propio
    // umbral — para aprovechar la visita técnica. Nunca se genera una orden de "General" sola.
    public static bool GeneralPairsWithUnits(MaintenanceSchedule schedule, AssetModel model, long currentCounter, DateTime asOf) =>
        UnitsProximity(schedule, model, currentCounter, asOf) <= ConsumablesProximity(schedule, model, currentCounter);

    // Predicción de "qué sigue" para mostrar en pantalla: cuál sub-regla está proporcionalmente más
    // próxima a vencer. A diferencia de EvaluateAsync (que solo genera una orden real dentro de la
    // ventana de anticipación de 15 días/5.000 impresiones), esto siempre devuelve una predicción — es
    // una aproximación consistente con esa misma lógica de emparejamiento, no una réplica exacta de su
    // gate de umbral absoluto.
    public static ComboResult DetermineLeadingCombo(MaintenanceSchedule schedule, AssetModel model, long currentCounter, DateTime asOf)
    {
        var generalProximity = GeneralProximity(schedule, model, currentCounter, asOf);
        var unitsProximity = UnitsProximity(schedule, model, currentCounter, asOf);
        var consumablesProximity = ConsumablesProximity(schedule, model, currentCounter);

        if (generalProximity <= unitsProximity && generalProximity <= consumablesProximity)
        {
            var pairWithUnits = unitsProximity <= consumablesProximity;
            return new ComboResult(true, pairWithUnits, !pairWithUnits, schedule.NextGeneralDueAt, schedule.NextGeneralDueCounter);
        }

        if (unitsProximity <= consumablesProximity)
        {
            return new ComboResult(false, true, false, schedule.NextUnitsDueAt, schedule.NextUnitsDueCounter);
        }

        return new ComboResult(false, false, true, null, schedule.NextConsumablesDueCounter);
    }
}
