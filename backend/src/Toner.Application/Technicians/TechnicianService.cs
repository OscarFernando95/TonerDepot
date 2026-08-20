using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Paging;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Technicians.Dtos;
using Toner.Domain.Entities;

namespace Toner.Application.Technicians;

public class TechnicianService : ITechnicianService
{
    private readonly IApplicationDbContext _db;

    public TechnicianService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<TechnicianDto>> ListAsync(int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        return await _db.Technicians
            .OrderBy(t => t.User.FullName)
            .Select(t => new TechnicianDto
            {
                Id = t.Id,
                FullName = t.User.FullName,
                Phone = t.Phone,
                Status = t.Status.ToString(),
                IsActive = t.IsActive,
                CoverageCityNames = t.Coverages.Select(c => c.City.Name).ToList()
            })
            .ToOffsetPageAsync(page, pageSize, cancellationToken);
    }

    public async Task<IReadOnlyList<TechnicianCoverageDto>> ListCoverageAsync(Guid technicianId, CancellationToken cancellationToken = default)
    {
        var technicianExists = await _db.Technicians.AnyAsync(t => t.Id == technicianId, cancellationToken);
        if (!technicianExists)
        {
            throw new NotFoundException(nameof(Technician), technicianId);
        }

        return await _db.TechnicianCoverages
            .Where(c => c.TechnicianId == technicianId)
            .OrderBy(c => c.City.Name)
            .Select(c => new TechnicianCoverageDto { Id = c.Id, CityId = c.CityId, CityName = c.City.Name })
            .ToListAsync(cancellationToken);
    }

    public async Task<TechnicianCoverageDto> AddCoverageAsync(Guid technicianId, AddTechnicianCoverageRequest request, CancellationToken cancellationToken = default)
    {
        var technicianExists = await _db.Technicians.AnyAsync(t => t.Id == technicianId, cancellationToken);
        if (!technicianExists)
        {
            throw new NotFoundException(nameof(Technician), technicianId);
        }

        var alreadyCovered = await _db.TechnicianCoverages
            .AnyAsync(c => c.TechnicianId == technicianId && c.CityId == request.CityId, cancellationToken);
        if (alreadyCovered)
        {
            throw new ConflictException("El técnico ya tiene cobertura registrada en esa ciudad.");
        }

        var coverage = new TechnicianCoverage { TechnicianId = technicianId, CityId = request.CityId };
        _db.TechnicianCoverages.Add(coverage);
        await _db.SaveChangesAsync(cancellationToken);

        return await _db.TechnicianCoverages
            .Where(c => c.Id == coverage.Id)
            .Select(c => new TechnicianCoverageDto { Id = c.Id, CityId = c.CityId, CityName = c.City.Name })
            .FirstAsync(cancellationToken);
    }

    public async Task RemoveCoverageAsync(Guid technicianId, Guid coverageId, CancellationToken cancellationToken = default)
    {
        var coverage = await _db.TechnicianCoverages
            .FirstOrDefaultAsync(c => c.Id == coverageId && c.TechnicianId == technicianId, cancellationToken)
            ?? throw new NotFoundException(nameof(TechnicianCoverage), coverageId);

        _db.TechnicianCoverages.Remove(coverage);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<TimeLogDto>> ListTimeLogsAsync(Guid technicianId, string? cursor, int? pageSize, CancellationToken cancellationToken = default)
    {
        var technicianExists = await _db.Technicians.AnyAsync(t => t.Id == technicianId, cancellationToken);
        if (!technicianExists)
        {
            throw new NotFoundException(nameof(Technician), technicianId);
        }

        var query = _db.TimeLogs.Where(tl => tl.TechnicianId == technicianId);

        var position = PageCursor.Decode(cursor);
        if (position is not null)
        {
            var (ts, lastId) = position.Value;
            query = query.Where(tl => tl.StartTime < ts || (tl.StartTime == ts && tl.Id.CompareTo(lastId) < 0));
        }

        return await query
            .OrderByDescending(tl => tl.StartTime).ThenByDescending(tl => tl.Id)
            .Select(tl => new TimeLogDto
            {
                Id = tl.Id,
                ServiceTicketId = tl.ServiceTicketId,
                MaintenanceOrderId = tl.MaintenanceOrderId,
                StartTime = tl.StartTime,
                EndTime = tl.EndTime,
                Notes = tl.Notes
            })
            .ToCursorPageAsync(pageSize, last => PageCursor.Encode(last.StartTime, last.Id), cancellationToken);
    }
}
