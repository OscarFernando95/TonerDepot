using Microsoft.EntityFrameworkCore;
using Toner.Application.Calendar;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Technicians.Dtos;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Technicians;

public class TechnicianHomeService : ITechnicianHomeService
{
    private const int AgendaSize = 10;
    private const int LowStockSize = 8;

    private readonly IApplicationDbContext _db;
    private readonly IWorkCalendarService _calendar;
    private readonly TimeProvider _time;

    public TechnicianHomeService(IApplicationDbContext db, IWorkCalendarService calendar, TimeProvider time)
    {
        _db = db;
        _calendar = calendar;
        _time = time;
    }

    public async Task<TechnicianHomeDto> GetAsync(Guid technicianId, CancellationToken cancellationToken = default)
    {
        if (!await _db.Technicians.AnyAsync(t => t.Id == technicianId, cancellationToken))
        {
            throw new NotFoundException(nameof(Technician), technicianId);
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_calendar.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, timeZone);
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(localNow.Date, timeZone);
        var calendar = await _calendar.LoadAsync(new[] { technicianId }, dayStartUtc, now.AddMinutes(1), cancellationToken);

        var home = new TechnicianHomeDto
        {
            IsWorkingNow = calendar.IsWorking(technicianId, now),
            TimeOffUntil = calendar.TimeOffEnd(technicianId, now),
            TodayShift = TodayShift(calendar, technicianId, localNow)
        };

        var zoneIds = await _db.TechnicianZones.Where(tz => tz.TechnicianId == technicianId).Select(tz => tz.ZoneId).ToListAsync(cancellationToken);
        home.ZoneNames = await _db.Zones.Where(z => zoneIds.Contains(z.Id)).OrderBy(z => z.Name).Select(z => z.Name).ToListAsync(cancellationToken);

        // Hoy (hora de la empresa): visitas cerradas y minutos trabajados.
        var closedToday = await _db.TimeLogs
            .Where(t => t.TechnicianId == technicianId && t.EndTime != null && t.EndTime >= dayStartUtc)
            .Select(t => new { t.StartTime, t.EndTime })
            .ToListAsync(cancellationToken);
        home.VisitsClosedToday = closedToday.Count;
        home.MinutesWorkedToday = (int)closedToday.Sum(t => (t.EndTime!.Value - (t.StartTime < dayStartUtc ? dayStartUtc : t.StartTime)).TotalMinutes);

        home.Agenda = await BuildAgendaAsync(technicianId, cancellationToken);
        await LoadActiveVisitAsync(technicianId, home, cancellationToken);
        home.LowStock = await LowStockAsync(zoneIds, cancellationToken);
        return home;
    }

    private static string? TodayShift(WorkCalendarContext calendar, Guid technicianId, DateTime localNow)
    {
        if (calendar.IsNonWorkingDay(DateOnly.FromDateTime(localNow)))
        {
            return null;
        }

        var spans = calendar.IntervalsFor(technicianId)
            .Where(i => i.Day == localNow.DayOfWeek)
            .OrderBy(i => i.Start)
            .Select(i => $"{i.Start:HH\\:mm}–{i.End:HH\\:mm}")
            .ToList();
        return spans.Count == 0 ? null : string.Join(", ", spans);
    }

    private async Task<IReadOnlyList<HomeJobDto>> BuildAgendaAsync(Guid technicianId, CancellationToken cancellationToken)
    {
        var tickets = await _db.ServiceTickets
            .Where(t => t.TechnicianId == technicianId && (t.Status == ServiceTicketStatus.Asignado || t.Status == ServiceTicketStatus.EnProceso))
            .Select(t => new
            {
                t.Id,
                Title = t.Asset != null ? t.Asset.AssetModel.AssetBrand.Name + " " + t.Asset.AssetModel.Name : (t.ExternalAssetBrand != null ? t.ExternalAssetBrand + " " + t.ExternalAssetModel : "Equipo sin catalogar"),
                Description = t.Description,
                ClientName = t.ClientLocation.Client.Name,
                LocationName = t.ClientLocation.Name,
                CityName = t.ClientLocation.City.Name,
                t.ClientLocation.Address,
                t.ClientLocation.Latitude,
                t.ClientLocation.Longitude,
                t.Priority,
                t.Status,
                t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var orders = await _db.MaintenanceOrders
            .Where(o => o.TechnicianId == technicianId && (o.Status == MaintenanceOrderStatus.Asignada || o.Status == MaintenanceOrderStatus.EnProceso))
            .Select(o => new
            {
                o.Id,
                Title = o.Asset.AssetModel.AssetBrand.Name + " " + o.Asset.AssetModel.Name,
                ClientName = o.Asset.CurrentClientLocation != null ? o.Asset.CurrentClientLocation.Client.Name : null,
                LocationName = o.Asset.CurrentClientLocation != null ? o.Asset.CurrentClientLocation.Name : null,
                CityName = o.Asset.CurrentClientLocation != null ? o.Asset.CurrentClientLocation.City.Name : null,
                Address = o.Asset.CurrentClientLocation != null ? o.Asset.CurrentClientLocation.Address : null,
                Latitude = o.Asset.CurrentClientLocation != null ? o.Asset.CurrentClientLocation.Latitude : null,
                Longitude = o.Asset.CurrentClientLocation != null ? o.Asset.CurrentClientLocation.Longitude : null,
                o.Status,
                o.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Instalaciones pendientes que este técnico puede tomar: las de los municipios que cubre, salvo las que otro
        // técnico ya tiene en curso (misma regla que su lista de instalaciones pendientes). La que él mismo tiene en
        // curso sí entra, marcada InProgress, igual que un ticket u orden iniciados.
        var coveredCityIds = await _db.CoveredCityIds(technicianId).ToListAsync(cancellationToken);
        var installations = coveredCityIds.Count == 0
            ? new List<InstallationRow>()
            : await _db.Assets
                .Where(a => a.LifecycleStatus == AssetLifecycleStatus.PendienteInstalacion
                    && a.CurrentClientLocation != null
                    && coveredCityIds.Contains(a.CurrentClientLocation.CityId)
                    && !_db.TimeLogs.Any(tl => tl.AssetId == a.Id && tl.EndTime == null && tl.TechnicianId != technicianId))
                .Select(a => new InstallationRow(
                    a.Id,
                    a.AssetModel.AssetBrand.Name + " " + a.AssetModel.Name,
                    a.SerialNumber,
                    a.CurrentClientLocation!.Client.Name,
                    a.CurrentClientLocation.Name,
                    a.CurrentClientLocation.City.Name,
                    a.CurrentClientLocation.Address,
                    a.CurrentClientLocation.Latitude,
                    a.CurrentClientLocation.Longitude,
                    _db.TimeLogs.Any(tl => tl.AssetId == a.Id && tl.EndTime == null && tl.TechnicianId == technicianId),
                    // Desde cuándo espera: el último paso a "pendiente de instalar" (la creación, si no hubo registro).
                    a.StatusLogs.Where(l => l.NewStatus == AssetLifecycleStatus.PendienteInstalacion).Max(l => (DateTime?)l.ChangedAt) ?? a.CreatedAt))
                .ToListAsync(cancellationToken);

        var jobs = tickets.Select(t => new HomeJobDto
            {
                Kind = "Ticket", Id = t.Id, Title = t.Title, Summary = t.Description.Length > 140 ? t.Description[..140] + "…" : t.Description, ClientName = t.ClientName, LocationName = t.LocationName,
                CityName = t.CityName, Address = t.Address, Latitude = t.Latitude, Longitude = t.Longitude,
                Priority = t.Priority.ToString(), Status = t.Status.ToString(), InProgress = t.Status == ServiceTicketStatus.EnProceso, Since = t.CreatedAt
            })
            .Concat(orders.Select(o => new HomeJobDto
            {
                Kind = "Orden", Id = o.Id, Title = o.Title, ClientName = o.ClientName, LocationName = o.LocationName,
                CityName = o.CityName, Address = o.Address, Latitude = o.Latitude, Longitude = o.Longitude,
                Priority = nameof(ServiceTicketPriority.Media), Status = o.Status.ToString(), InProgress = o.Status == MaintenanceOrderStatus.EnProceso, Since = o.CreatedAt
            }))
            .Concat(installations.Select(i => new HomeJobDto
            {
                Kind = "Instalación", Id = i.Id, Title = i.Title, Summary = $"Instalación pendiente · serie {i.SerialNumber}", ClientName = i.ClientName,
                LocationName = i.LocationName, CityName = i.CityName, Address = i.Address, Latitude = i.Latitude, Longitude = i.Longitude,
                // Sin criticidad propia (como las órdenes): se ordena como Media y la app no muestra la etiqueta.
                Priority = nameof(ServiceTicketPriority.Media), Status = i.InProgress ? "EnProceso" : "Pendiente", InProgress = i.InProgress, Since = i.Since
            }));

        // En curso primero, luego por prioridad (Crítica → Baja) y, a igual prioridad, lo más antiguo.
        return jobs
            .OrderByDescending(j => j.InProgress)
            .ThenByDescending(j => Enum.Parse<ServiceTicketPriority>(j.Priority))
            .ThenBy(j => j.Since)
            .Take(AgendaSize)
            .ToList();
    }

    private sealed record InstallationRow(
        Guid Id, string Title, string SerialNumber, string ClientName, string LocationName, string CityName, string? Address,
        double? Latitude, double? Longitude, bool InProgress, DateTime Since);

    private async Task LoadActiveVisitAsync(Guid technicianId, TechnicianHomeDto home, CancellationToken cancellationToken)
    {
        var open = await _db.TimeLogs
            .Where(t => t.TechnicianId == technicianId && t.EndTime == null)
            .OrderByDescending(t => t.StartTime)
            .Select(t => new { t.ServiceTicketId, t.MaintenanceOrderId, t.AssetId, t.StartTime })
            .FirstOrDefaultAsync(cancellationToken);
        if (open is null)
        {
            return;
        }

        home.ActiveVisitStartedAt = open.StartTime;

        // La visita en curso aparece también en la agenda (en curso primero). Una instalación cuya sede ya no cubre el
        // técnico queda fuera de la agenda: ahí se arma aparte.
        home.ActiveVisit = open.ServiceTicketId.HasValue
            ? home.Agenda.FirstOrDefault(j => j.Kind == "Ticket" && j.Id == open.ServiceTicketId.Value)
            : open.MaintenanceOrderId.HasValue
                ? home.Agenda.FirstOrDefault(j => j.Kind == "Orden" && j.Id == open.MaintenanceOrderId.Value)
                : open.AssetId.HasValue
                    ? home.Agenda.FirstOrDefault(j => j.Kind == "Instalación" && j.Id == open.AssetId.Value)
                    : null;

        if (home.ActiveVisit is null && open.AssetId.HasValue)
        {
            var asset = await _db.Assets
                .Where(a => a.Id == open.AssetId.Value)
                .Select(a => new
                {
                    Title = a.AssetModel.AssetBrand.Name + " " + a.AssetModel.Name,
                    ClientName = a.CurrentClientLocation != null ? a.CurrentClientLocation.Client.Name : null,
                    LocationName = a.CurrentClientLocation != null ? a.CurrentClientLocation.Name : null,
                    CityName = a.CurrentClientLocation != null ? a.CurrentClientLocation.City.Name : null,
                    Address = a.CurrentClientLocation != null ? a.CurrentClientLocation.Address : null,
                    Latitude = a.CurrentClientLocation != null ? a.CurrentClientLocation.Latitude : null,
                    Longitude = a.CurrentClientLocation != null ? a.CurrentClientLocation.Longitude : null
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (asset is not null)
            {
                home.ActiveVisit = new HomeJobDto
                {
                    Kind = "Instalación", Id = open.AssetId.Value, Title = asset.Title, ClientName = asset.ClientName, LocationName = asset.LocationName,
                    CityName = asset.CityName, Address = asset.Address, Latitude = asset.Latitude, Longitude = asset.Longitude,
                    Status = "EnProceso", InProgress = true, Since = open.StartTime
                };
            }
        }
    }

    // Piezas por debajo de su mínimo (o en negativo) en el inventario de las zonas del técnico.
    private async Task<IReadOnlyList<HomeLowStockDto>> LowStockAsync(IReadOnlyCollection<Guid> zoneIds, CancellationToken cancellationToken)
    {
        if (zoneIds.Count == 0)
        {
            return Array.Empty<HomeLowStockDto>();
        }

        var balances = _db.InventoryMovements
            .Where(m => m.InventoryLocation.ZoneId != null && zoneIds.Contains(m.InventoryLocation.ZoneId.Value))
            .GroupBy(m => new { m.InventoryLocationId, m.InventoryItemId })
            .Select(g => new { g.Key.InventoryLocationId, g.Key.InventoryItemId, Quantity = g.Sum(m => m.Delta) });

        return await (from b in balances
                      join i in _db.InventoryItems on b.InventoryItemId equals i.Id
                      join l in _db.InventoryLocations on b.InventoryLocationId equals l.Id
                      where i.IsActive && (b.Quantity < 0 || (i.MinimumStock > 0 && b.Quantity <= i.MinimumStock))
                      orderby b.Quantity, i.Name
                      select new HomeLowStockDto { ItemName = i.Name, LocationName = l.Zone!.Name, Quantity = b.Quantity, MinimumStock = i.MinimumStock })
            .Take(LowStockSize)
            .ToListAsync(cancellationToken);
    }
}
