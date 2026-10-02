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
                ZoneNames = t.TechnicianZones.Select(tz => tz.Zone.Name).OrderBy(n => n).ToList(),
                CoverageCityNames = t.TechnicianZones.SelectMany(tz => tz.Zone.Cities).Select(c => c.Name).Distinct().OrderBy(n => n).ToList()
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

    public async Task<IReadOnlyList<TechnicianZoneDto>> ListZonesAsync(Guid technicianId, CancellationToken cancellationToken = default)
    {
        await EnsureTechnicianExistsAsync(technicianId, cancellationToken);
        return await ZonesOf(technicianId).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TechnicianZoneDto>> SetZonesAsync(
        Guid technicianId, SetTechnicianZonesRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureTechnicianExistsAsync(technicianId, cancellationToken);

        var wanted = request.ZoneIds.Distinct().ToList();
        var current = await _db.TechnicianZones.Where(tz => tz.TechnicianId == technicianId).ToListAsync(cancellationToken);

        _db.TechnicianZones.RemoveRange(current.Where(tz => !wanted.Contains(tz.ZoneId)));
        foreach (var zoneId in wanted.Where(id => current.All(tz => tz.ZoneId != id)))
        {
            _db.TechnicianZones.Add(new TechnicianZone { TechnicianId = technicianId, ZoneId = zoneId });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await ZonesOf(technicianId).ToListAsync(cancellationToken);
    }

    private async Task EnsureTechnicianExistsAsync(Guid technicianId, CancellationToken cancellationToken)
    {
        if (!await _db.Technicians.AnyAsync(t => t.Id == technicianId, cancellationToken))
        {
            throw new NotFoundException(nameof(Technician), technicianId);
        }
    }

    private IQueryable<TechnicianZoneDto> ZonesOf(Guid technicianId) =>
        _db.TechnicianZones
            .Where(tz => tz.TechnicianId == technicianId)
            .OrderBy(tz => tz.Zone.Name)
            .Select(tz => new TechnicianZoneDto
            {
                ZoneId = tz.ZoneId,
                ZoneName = tz.Zone.Name,
                CityNames = tz.Zone.Cities.OrderBy(c => c.Name).Select(c => c.Name).ToList()
            });

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
