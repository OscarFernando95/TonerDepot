using Microsoft.EntityFrameworkCore;
using Toner.Application.Calendar;
using Toner.Application.Common.Paging;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Technicians.Dtos;
using Toner.Domain.Entities;

namespace Toner.Application.Technicians;

public class TechnicianService : ITechnicianService
{
    private readonly IApplicationDbContext _db;
    private readonly IWorkCalendarService _calendar;
    private readonly TimeProvider _time;

    public TechnicianService(IApplicationDbContext db, IWorkCalendarService calendar, TimeProvider time)
    {
        _db = db;
        _calendar = calendar;
        _time = time;
    }

    public async Task<PagedResult<TechnicianDto>> ListAsync(int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        var result = await _db.Technicians
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

        // Disponibilidad calculada al momento (no se persiste): horario + festivos + fuera de la oficina.
        var now = _time.GetUtcNow().UtcDateTime;
        var calendar = await _calendar.LoadAsync(result.Items.Select(t => t.Id), now, now.AddMinutes(1), cancellationToken);
        foreach (var technician in result.Items)
        {
            technician.IsWorkingNow = calendar.IsWorking(technician.Id, now);
            technician.TimeOffUntil = calendar.TimeOffEnd(technician.Id, now);
        }

        return result;
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

    public async Task<IReadOnlyList<TechnicianAssetDto>> ListLinkedAssetsAsync(Guid technicianId, CancellationToken cancellationToken = default)
    {
        var technicianExists = await _db.Technicians.AnyAsync(t => t.Id == technicianId, cancellationToken);
        if (!technicianExists)
        {
            throw new NotFoundException(nameof(Technician), technicianId);
        }

        return await _db.TechnicianAssets
            .Where(ta => ta.TechnicianId == technicianId)
            .OrderBy(ta => ta.Asset.CurrentClientLocation!.Client.Name)
            .Select(ta => new TechnicianAssetDto
            {
                Id = ta.Id,
                AssetId = ta.AssetId,
                AssetBrandName = ta.Asset.AssetModel.AssetBrand.Name,
                Model = ta.Asset.AssetModel.Name,
                SerialNumber = ta.Asset.SerialNumber,
                ClientName = ta.Asset.CurrentClientLocation != null ? ta.Asset.CurrentClientLocation.Client.Name : null,
                ClientLocationName = ta.Asset.CurrentClientLocation != null ? ta.Asset.CurrentClientLocation.Name : null,
                CityName = ta.Asset.CurrentClientLocation != null ? ta.Asset.CurrentClientLocation.City.Name : null,
                Area = ta.Asset.Area
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<TechnicianAssetDto> LinkAssetAsync(Guid technicianId, AddTechnicianAssetRequest request, CancellationToken cancellationToken = default)
    {
        var technicianExists = await _db.Technicians.AnyAsync(t => t.Id == technicianId, cancellationToken);
        if (!technicianExists)
        {
            throw new NotFoundException(nameof(Technician), technicianId);
        }

        var alreadyLinked = await _db.TechnicianAssets
            .AnyAsync(ta => ta.TechnicianId == technicianId && ta.AssetId == request.AssetId, cancellationToken);
        if (alreadyLinked)
        {
            throw new ConflictException("El técnico ya tiene vinculado ese activo.");
        }

        var link = new TechnicianAsset { TechnicianId = technicianId, AssetId = request.AssetId };
        _db.TechnicianAssets.Add(link);
        await _db.SaveChangesAsync(cancellationToken);

        return await _db.TechnicianAssets
            .Where(ta => ta.Id == link.Id)
            .Select(ta => new TechnicianAssetDto
            {
                Id = ta.Id,
                AssetId = ta.AssetId,
                AssetBrandName = ta.Asset.AssetModel.AssetBrand.Name,
                Model = ta.Asset.AssetModel.Name,
                SerialNumber = ta.Asset.SerialNumber,
                ClientName = ta.Asset.CurrentClientLocation != null ? ta.Asset.CurrentClientLocation.Client.Name : null,
                ClientLocationName = ta.Asset.CurrentClientLocation != null ? ta.Asset.CurrentClientLocation.Name : null,
                CityName = ta.Asset.CurrentClientLocation != null ? ta.Asset.CurrentClientLocation.City.Name : null,
                Area = ta.Asset.Area
            })
            .FirstAsync(cancellationToken);
    }

    public async Task UnlinkAssetAsync(Guid technicianId, Guid technicianAssetId, CancellationToken cancellationToken = default)
    {
        var link = await _db.TechnicianAssets
            .FirstOrDefaultAsync(ta => ta.Id == technicianAssetId && ta.TechnicianId == technicianId, cancellationToken)
            ?? throw new NotFoundException(nameof(TechnicianAsset), technicianAssetId);

        _db.TechnicianAssets.Remove(link);
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
                Notes = tl.Notes,
                CheckInLocationStatus = tl.CheckInLocationStatus != null ? tl.CheckInLocationStatus.ToString() : null,
                CheckInDistanceMeters = tl.CheckInDistanceMeters,
                CheckOutLocationStatus = tl.CheckOutLocationStatus != null ? tl.CheckOutLocationStatus.ToString() : null,
                CheckOutDistanceMeters = tl.CheckOutDistanceMeters
            })
            .ToCursorPageAsync(pageSize, last => PageCursor.Encode(last.StartTime, last.Id), cancellationToken);
    }
}
