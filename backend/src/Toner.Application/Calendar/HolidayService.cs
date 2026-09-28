using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Calendar.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;

namespace Toner.Application.Calendar;

public class HolidayService : IHolidayService
{
    private readonly IApplicationDbContext _db;

    public HolidayService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<HolidayDto>> ListAsync(int year, CancellationToken cancellationToken = default)
    {
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        var overrides = await _db.CompanyHolidayOverrides
            .Where(h => h.Date >= from && h.Date <= to)
            .ToDictionaryAsync(h => h.Date, cancellationToken);

        var result = new List<HolidayDto>();
        foreach (var (date, name) in ColombiaHolidays.ForYear(year))
        {
            result.Add(new HolidayDto
            {
                Date = Format(date),
                Name = name,
                Source = "legal",
                IsWorkingDay = overrides.TryGetValue(date, out var o) && o.IsWorkingDay
            });
        }

        var legal = ColombiaHolidays.ForYear(year);
        foreach (var custom in overrides.Values.Where(o => !o.IsWorkingDay && !legal.ContainsKey(o.Date)))
        {
            result.Add(new HolidayDto { Date = Format(custom.Date), Name = custom.Name, Source = "custom", IsWorkingDay = false });
        }

        return result.OrderBy(h => h.Date, StringComparer.Ordinal).ToList();
    }

    public async Task<HolidayDto> SetOverrideAsync(SetHolidayOverrideRequest request, CancellationToken cancellationToken = default)
    {
        var date = DateOnly.ParseExact(request.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var isLegal = ColombiaHolidays.ForYear(date.Year).ContainsKey(date);

        if (request.IsWorkingDay && !isLegal)
        {
            throw new ConflictException("Solo se puede marcar como laborable un festivo legal; esa fecha ya es un día normal.");
        }

        var existing = await _db.CompanyHolidayOverrides.FirstOrDefaultAsync(h => h.Date == date, cancellationToken);
        if (existing is null)
        {
            existing = new CompanyHolidayOverride { Date = date };
            _db.CompanyHolidayOverrides.Add(existing);
        }

        existing.Name = request.Name.Trim();
        existing.IsWorkingDay = request.IsWorkingDay;
        await _db.SaveChangesAsync(cancellationToken);

        return new HolidayDto
        {
            Date = Format(date),
            Name = isLegal ? ColombiaHolidays.ForYear(date.Year)[date] : existing.Name,
            Source = isLegal ? "legal" : "custom",
            IsWorkingDay = existing.IsWorkingDay
        };
    }

    public async Task RemoveOverrideAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var existing = await _db.CompanyHolidayOverrides.FirstOrDefaultAsync(h => h.Date == date, cancellationToken)
            ?? throw new NotFoundException(nameof(CompanyHolidayOverride), date);

        _db.CompanyHolidayOverrides.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
