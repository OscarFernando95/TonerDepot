using Toner.Domain.Enums;

namespace Toner.Application.Calendar;

public sealed record WorkInterval(DayOfWeek Day, TimeOnly Start, TimeOnly End);

// Período efectivo de "fuera de la oficina" (ya recortado por una cancelación anticipada).
public sealed record TimeOffPeriod(DateTime StartUtc, DateTime EndUtc);

// Calculadora pura (sin base de datos) de disponibilidad y horas hábiles. Se construye con los datos ya
// cargados (ver WorkCalendarService) para poder evaluar muchos técnicos/tickets sin una consulta por cada uno.
public sealed class WorkCalendarContext
{
    private readonly TimeZoneInfo _timeZone;
    private readonly Func<DateOnly, bool> _isNonWorkingDay;
    private readonly IReadOnlyList<WorkInterval> _defaultIntervals;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<WorkInterval>> _intervalsByTechnician;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<TimeOffPeriod>> _timeOffByTechnician;

    public WorkCalendarContext(
        TimeZoneInfo timeZone,
        Func<DateOnly, bool> isNonWorkingDay,
        IReadOnlyList<WorkInterval> defaultIntervals,
        IReadOnlyDictionary<Guid, IReadOnlyList<WorkInterval>> intervalsByTechnician,
        IReadOnlyDictionary<Guid, IReadOnlyList<TimeOffPeriod>> timeOffByTechnician)
    {
        _timeZone = timeZone;
        _isNonWorkingDay = isNonWorkingDay;
        _defaultIntervals = defaultIntervals;
        _intervalsByTechnician = intervalsByTechnician;
        _timeOffByTechnician = timeOffByTechnician;
    }

    public IReadOnlyList<WorkInterval> IntervalsFor(Guid? technicianId) =>
        technicianId is { } id && _intervalsByTechnician.TryGetValue(id, out var own) ? own : _defaultIntervals;

    public bool IsInTimeOff(Guid technicianId, DateTime instantUtc) =>
        _timeOffByTechnician.TryGetValue(technicianId, out var periods)
        && periods.Any(p => p.StartUtc <= instantUtc && instantUtc < p.EndUtc);

    // Hasta cuándo dura el "fuera de la oficina" vigente en ese instante, o null si no está fuera.
    public DateTime? TimeOffEnd(Guid technicianId, DateTime instantUtc) =>
        _timeOffByTechnician.TryGetValue(technicianId, out var periods)
            ? periods.FirstOrDefault(p => p.StartUtc <= instantUtc && instantUtc < p.EndUtc)?.EndUtc
            : null;

    // ¿Está en su horario laboral (día hábil, dentro de un tramo, sin permiso)? Es lo que se muestra como
    // "disponible ahora" para un cliente en horario de oficina.
    public bool IsWorking(Guid technicianId, DateTime instantUtc)
    {
        if (IsInTimeOff(technicianId, instantUtc))
        {
            return false;
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(instantUtc, _timeZone);
        if (_isNonWorkingDay(DateOnly.FromDateTime(local)))
        {
            return false;
        }

        var time = TimeOnly.FromDateTime(local);
        return IntervalsFor(technicianId).Any(i => i.Day == local.DayOfWeek && i.Start <= time && time < i.End);
    }

    // ¿Se le puede asignar trabajo de un cliente con esa cobertura ahora mismo?
    // 24/7: da igual su horario, pero un permiso/vacaciones sí lo excluye.
    public bool IsAssignable(Guid technicianId, SupportCoverage coverage, DateTime instantUtc) =>
        coverage == SupportCoverage.Continuo24x7 ? !IsInTimeOff(technicianId, instantUtc) : IsWorking(technicianId, instantUtc);

    // Horas del rango que cuentan para el SLA. 24/7: horas corridas. Horario de oficina: solo horas dentro
    // del horario del técnico (o el de la empresa si no hay técnico), sin festivos ni permisos.
    public double BusinessHours(Guid? technicianId, SupportCoverage coverage, DateTime fromUtc, DateTime toUtc)
    {
        if (toUtc <= fromUtc)
        {
            return 0;
        }

        if (coverage == SupportCoverage.Continuo24x7)
        {
            return (toUtc - fromUtc).TotalHours;
        }

        var intervals = IntervalsFor(technicianId);
        var offPeriods = technicianId is { } id && _timeOffByTechnician.TryGetValue(id, out var periods)
            ? periods
            : Array.Empty<TimeOffPeriod>();

        var fromLocal = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, _timeZone);
        var toLocal = TimeZoneInfo.ConvertTimeFromUtc(toUtc, _timeZone);
        var total = TimeSpan.Zero;

        for (var date = DateOnly.FromDateTime(fromLocal); date <= DateOnly.FromDateTime(toLocal); date = date.AddDays(1))
        {
            if (_isNonWorkingDay(date))
            {
                continue;
            }

            foreach (var interval in intervals.Where(i => i.Day == date.DayOfWeek))
            {
                var startUtc = ToUtc(date, interval.Start);
                var endUtc = ToUtc(date, interval.End);
                var clippedStart = startUtc > fromUtc ? startUtc : fromUtc;
                var clippedEnd = endUtc < toUtc ? endUtc : toUtc;
                if (clippedEnd <= clippedStart)
                {
                    continue;
                }

                total += SubtractTimeOff(clippedStart, clippedEnd, offPeriods);
            }
        }

        return total.TotalHours;
    }

    private DateTime ToUtc(DateOnly date, TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, _timeZone);
    }

    private static TimeSpan SubtractTimeOff(DateTime start, DateTime end, IReadOnlyList<TimeOffPeriod> periods)
    {
        var remaining = end - start;
        foreach (var period in periods)
        {
            var overlapStart = period.StartUtc > start ? period.StartUtc : start;
            var overlapEnd = period.EndUtc < end ? period.EndUtc : end;
            if (overlapEnd > overlapStart)
            {
                remaining -= overlapEnd - overlapStart;
            }
        }

        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }
}
