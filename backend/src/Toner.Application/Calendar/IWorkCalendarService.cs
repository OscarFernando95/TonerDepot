namespace Toner.Application.Calendar;

public interface IWorkCalendarService
{
    // Carga en bloque (festivos, horarios y permisos) lo necesario para evaluar a estos técnicos entre
    // fromUtc y toUtc, sin una consulta por técnico/ticket.
    Task<WorkCalendarContext> LoadAsync(
        IEnumerable<Guid> technicianIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    // Horario laboral efectivo por defecto (opciones de la empresa).
    IReadOnlyList<WorkInterval> DefaultIntervals { get; }
    string TimeZoneId { get; }
}
