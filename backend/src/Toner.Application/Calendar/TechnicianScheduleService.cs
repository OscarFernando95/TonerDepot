using System.Globalization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Toner.Application.Calendar.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;

namespace Toner.Application.Calendar;

public class TechnicianScheduleService : ITechnicianScheduleService
{
    private readonly IApplicationDbContext _db;
    private readonly IWorkCalendarService _calendar;
    private readonly TimeProvider _time;
    private readonly ILogger<TechnicianScheduleService> _logger;

    public TechnicianScheduleService(
        IApplicationDbContext db, IWorkCalendarService calendar, TimeProvider time, ILogger<TechnicianScheduleService> logger)
    {
        _db = db;
        _calendar = calendar;
        _time = time;
        _logger = logger;
    }

    public async Task<TechnicianScheduleDto> GetScheduleAsync(Guid technicianId, CancellationToken cancellationToken = default)
    {
        await EnsureTechnicianExistsAsync(technicianId, cancellationToken);
        return await BuildScheduleDtoAsync(technicianId, cancellationToken);
    }

    public async Task<TechnicianScheduleDto> SetScheduleAsync(Guid technicianId, SetTechnicianScheduleRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureTechnicianExistsAsync(technicianId, cancellationToken);

        var existing = await _db.TechnicianWorkIntervals.Where(i => i.TechnicianId == technicianId).ToListAsync(cancellationToken);
        _db.TechnicianWorkIntervals.RemoveRange(existing);

        foreach (var interval in request.Intervals)
        {
            _db.TechnicianWorkIntervals.Add(new TechnicianWorkInterval
            {
                TechnicianId = technicianId,
                Day = (DayOfWeek)interval.Day,
                StartTime = TimeOnly.ParseExact(interval.Start, "HH:mm", CultureInfo.InvariantCulture),
                EndTime = TimeOnly.ParseExact(interval.End, "HH:mm", CultureInfo.InvariantCulture)
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await BuildScheduleDtoAsync(technicianId, cancellationToken);
    }

    public async Task<TechnicianScheduleDto> ResetScheduleAsync(Guid technicianId, CancellationToken cancellationToken = default)
    {
        await EnsureTechnicianExistsAsync(technicianId, cancellationToken);

        var existing = await _db.TechnicianWorkIntervals.Where(i => i.TechnicianId == technicianId).ToListAsync(cancellationToken);
        _db.TechnicianWorkIntervals.RemoveRange(existing);
        await _db.SaveChangesAsync(cancellationToken);

        return await BuildScheduleDtoAsync(technicianId, cancellationToken);
    }

    public async Task<IReadOnlyList<TimeOffDto>> ListTimeOffAsync(Guid technicianId, CancellationToken cancellationToken = default)
    {
        await EnsureTechnicianExistsAsync(technicianId, cancellationToken);

        var rows = await _db.TechnicianTimeOffs
            .Where(t => t.TechnicianId == technicianId)
            .OrderByDescending(t => t.StartsAt)
            .Take(30)
            .ToListAsync(cancellationToken);

        var now = _time.GetUtcNow().UtcDateTime;
        return rows.Select(t => ToDto(t, now)).ToList();
    }

    public async Task<TimeOffDto> AddTimeOffAsync(Guid technicianId, CreateTimeOffRequest request, Guid performedByUserId, CancellationToken cancellationToken = default)
    {
        await EnsureTechnicianExistsAsync(technicianId, cancellationToken);

        var now = _time.GetUtcNow().UtcDateTime;
        var startsAt = request.StartsAt.ToUniversalTime();
        var endsAt = request.EndsAt.ToUniversalTime();

        if (endsAt <= now)
        {
            throw new ValidationException("La fecha de fin ya pasó: el período fuera de la oficina debe terminar en el futuro.");
        }

        var overlaps = await _db.TechnicianTimeOffs.AnyAsync(
            t => t.TechnicianId == technicianId
                && t.StartsAt < endsAt
                && t.EndsAt > startsAt
                && (t.CancelledAt == null || t.CancelledAt > startsAt),
            cancellationToken);
        if (overlaps)
        {
            throw new ConflictException("El técnico ya tiene un período fuera de la oficina que se cruza con ese rango.");
        }

        var timeOff = new TechnicianTimeOff
        {
            TechnicianId = technicianId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            CreatedByUserId = performedByUserId
        };
        _db.TechnicianTimeOffs.Add(timeOff);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Técnico {TechnicianId} marcado fuera de la oficina por {PerformedByUserId}: {StartsAt:o} a {EndsAt:o}",
            technicianId, performedByUserId, startsAt, endsAt);

        return ToDto(timeOff, now);
    }

    public async Task<TimeOffDto> CancelTimeOffAsync(Guid technicianId, Guid timeOffId, CancellationToken cancellationToken = default)
    {
        var timeOff = await _db.TechnicianTimeOffs.FirstOrDefaultAsync(t => t.Id == timeOffId && t.TechnicianId == technicianId, cancellationToken)
            ?? throw new NotFoundException(nameof(TechnicianTimeOff), timeOffId);

        var now = _time.GetUtcNow().UtcDateTime;
        if (timeOff.CancelledAt is not null || timeOff.EndsAt <= now)
        {
            throw new ConflictException("Ese período fuera de la oficina ya terminó o fue cancelado.");
        }

        // Si aún no empezó se anula por completo (queda vacío); si está en curso, termina ahora.
        timeOff.CancelledAt = timeOff.StartsAt > now ? timeOff.StartsAt : now;
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(timeOff, now);
    }

    private async Task<TechnicianScheduleDto> BuildScheduleDtoAsync(Guid technicianId, CancellationToken cancellationToken)
    {
        var own = await _db.TechnicianWorkIntervals
            .Where(i => i.TechnicianId == technicianId)
            .OrderBy(i => i.Day).ThenBy(i => i.StartTime)
            .ToListAsync(cancellationToken);

        var intervals = own.Count > 0
            ? own.Select(i => new WorkInterval(i.Day, i.StartTime, i.EndTime)).ToList()
            : _calendar.DefaultIntervals.ToList();

        return new TechnicianScheduleDto
        {
            IsDefault = own.Count == 0,
            TimeZoneId = _calendar.TimeZoneId,
            Intervals = intervals.Select(i => new WorkIntervalDto
            {
                Day = (int)i.Day,
                Start = i.Start.ToString("HH:mm", CultureInfo.InvariantCulture),
                End = i.End.ToString("HH:mm", CultureInfo.InvariantCulture)
            }).ToList()
        };
    }

    private async Task EnsureTechnicianExistsAsync(Guid technicianId, CancellationToken cancellationToken)
    {
        if (!await _db.Technicians.AnyAsync(t => t.Id == technicianId, cancellationToken))
        {
            throw new NotFoundException(nameof(Technician), technicianId);
        }
    }

    private static TimeOffDto ToDto(TechnicianTimeOff t, DateTime now)
    {
        var effectiveEnd = t.CancelledAt is { } cancelled && cancelled < t.EndsAt ? cancelled : t.EndsAt;
        return new TimeOffDto
        {
            Id = t.Id,
            StartsAt = t.StartsAt,
            EndsAt = t.EndsAt,
            EffectiveEndsAt = effectiveEnd,
            Reason = t.Reason,
            CancelledAt = t.CancelledAt,
            IsActive = t.StartsAt <= now && now < effectiveEnd
        };
    }
}
