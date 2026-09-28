using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Toner.Application.Common.Interfaces;

namespace Toner.Application.Calendar;

public class WorkCalendarService : IWorkCalendarService
{
    private readonly IApplicationDbContext _db;
    private readonly WorkCalendarOptions _options;
    private readonly TimeZoneInfo _timeZone;

    public WorkCalendarService(IApplicationDbContext db, IOptions<WorkCalendarOptions> options)
    {
        _db = db;
        _options = options.Value;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZoneId);
        DefaultIntervals = BuildDefaultIntervals(_options);
    }

    public IReadOnlyList<WorkInterval> DefaultIntervals { get; }

    public string TimeZoneId => _options.TimeZoneId;

    public async Task<WorkCalendarContext> LoadAsync(
        IEnumerable<Guid> technicianIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var ids = technicianIds.Distinct().ToList();

        var overrides = await _db.CompanyHolidayOverrides.ToDictionaryAsync(h => h.Date, h => h.IsWorkingDay, cancellationToken);
        var legalByYear = new Dictionary<int, IReadOnlyDictionary<DateOnly, string>>();

        bool IsNonWorkingDay(DateOnly date)
        {
            if (overrides.TryGetValue(date, out var isWorkingDay))
            {
                return !isWorkingDay;
            }

            if (!legalByYear.TryGetValue(date.Year, out var legal))
            {
                legal = ColombiaHolidays.ForYear(date.Year);
                legalByYear[date.Year] = legal;
            }

            return legal.ContainsKey(date);
        }

        var intervalRows = await _db.TechnicianWorkIntervals
            .Where(i => ids.Contains(i.TechnicianId))
            .ToListAsync(cancellationToken);
        var intervals = intervalRows
            .GroupBy(i => i.TechnicianId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<WorkInterval>)g.Select(i => new WorkInterval(i.Day, i.StartTime, i.EndTime)).ToList());

        var offRows = await _db.TechnicianTimeOffs
            .Where(t => ids.Contains(t.TechnicianId) && t.StartsAt < toUtc && t.EndsAt > fromUtc)
            .ToListAsync(cancellationToken);
        var timeOffs = offRows
            .GroupBy(t => t.TechnicianId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TimeOffPeriod>)Merge(g.Select(Effective)));

        return new WorkCalendarContext(_timeZone, IsNonWorkingDay, DefaultIntervals, intervals, timeOffs);
    }

    // Una cancelación anticipada acorta el período; si se canceló antes de empezar, queda vacío.
    private static TimeOffPeriod Effective(Domain.Entities.TechnicianTimeOff t) =>
        new(t.StartsAt, t.CancelledAt is { } cancelled && cancelled < t.EndsAt ? cancelled : t.EndsAt);

    // Une períodos solapados para que restar horas no cuente dos veces el mismo tiempo.
    private static List<TimeOffPeriod> Merge(IEnumerable<TimeOffPeriod> periods)
    {
        var merged = new List<TimeOffPeriod>();
        foreach (var period in periods.Where(p => p.EndUtc > p.StartUtc).OrderBy(p => p.StartUtc))
        {
            if (merged.Count > 0 && period.StartUtc <= merged[^1].EndUtc)
            {
                merged[^1] = merged[^1] with { EndUtc = period.EndUtc > merged[^1].EndUtc ? period.EndUtc : merged[^1].EndUtc };
            }
            else
            {
                merged.Add(period);
            }
        }

        return merged;
    }

    private static List<WorkInterval> BuildDefaultIntervals(WorkCalendarOptions options)
    {
        var start = TimeOnly.Parse(options.DefaultWorkStart);
        var end = TimeOnly.Parse(options.DefaultWorkEnd);
        var days = options.DefaultWorkDays.Length > 0 ? options.DefaultWorkDays : new[] { 1, 2, 3, 4, 5 };
        return days.Distinct().Select(day => new WorkInterval((DayOfWeek)day, start, end)).ToList();
    }
}
