using Microsoft.EntityFrameworkCore;
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

    public async Task<IReadOnlyList<TechnicianDto>> ListAsync(CancellationToken cancellationToken = default)
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
            .ToListAsync(cancellationToken);
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

    public async Task<IReadOnlyList<TimeLogDto>> ListTimeLogsAsync(Guid technicianId, CancellationToken cancellationToken = default)
    {
        var technicianExists = await _db.Technicians.AnyAsync(t => t.Id == technicianId, cancellationToken);
        if (!technicianExists)
        {
            throw new NotFoundException(nameof(Technician), technicianId);
        }

        return await _db.TimeLogs
            .Where(tl => tl.TechnicianId == technicianId)
            .OrderByDescending(tl => tl.StartTime)
            .Select(tl => new TimeLogDto
            {
                Id = tl.Id,
                ServiceTicketId = tl.ServiceTicketId,
                MaintenanceOrderId = tl.MaintenanceOrderId,
                StartTime = tl.StartTime,
                EndTime = tl.EndTime,
                Notes = tl.Notes
            })
            .ToListAsync(cancellationToken);
    }
}
