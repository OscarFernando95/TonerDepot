using Microsoft.EntityFrameworkCore;
using Toner.Application.ClientPortal.Dtos;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.ClientPortal;

public class ClientPortalService : IClientPortalService
{
    private const int ListSize = 5;

    private static readonly ServiceTicketStatus[] OpenTicketStatuses =
    {
        ServiceTicketStatus.Abierto, ServiceTicketStatus.SinAsignar, ServiceTicketStatus.Asignado, ServiceTicketStatus.EnProceso
    };

    private static readonly MaintenanceOrderStatus[] OpenOrderStatuses =
    {
        MaintenanceOrderStatus.Pendiente, MaintenanceOrderStatus.Asignada, MaintenanceOrderStatus.EnProceso
    };

    private readonly IApplicationDbContext _db;

    public ClientPortalService(IApplicationDbContext db) => _db = db;

    public async Task<ClientHomeDto> GetHomeAsync(RequestingUser requestingUser, CancellationToken cancellationToken = default)
    {
        if (!requestingUser.IsClient)
        {
            throw new ForbiddenException("El inicio del cliente es solo para usuarios Cliente.");
        }

        var clientId = requestingUser.RequireClientId();
        var allowed = await _db.AllowedLocationIdsAsync(requestingUser, cancellationToken);

        // Cada consulta parte del aislamiento por empresa y suma el alcance por sede si lo hay (mismo criterio que las listas).
        var locations = _db.ClientLocations.Where(l => l.ClientId == clientId && l.IsActive);
        var tickets = _db.ServiceTickets.Where(t => t.ClientId == clientId);
        var orders = _db.MaintenanceOrders.Where(o => o.ClientId == clientId);
        var assets = _db.Assets.Where(a => a.CurrentClientLocation != null && a.CurrentClientLocation.ClientId == clientId);
        if (allowed is not null)
        {
            locations = locations.Where(l => allowed.Contains(l.Id));
            tickets = tickets.Where(t => allowed.Contains(t.ClientLocationId));
            orders = orders.Where(o => o.Asset.CurrentClientLocationId != null && allowed.Contains(o.Asset.CurrentClientLocationId.Value));
            assets = assets.Where(a => allowed.Contains(a.CurrentClientLocationId!.Value));
        }

        var pendingInstallations = assets.Where(a => a.LifecycleStatus == AssetLifecycleStatus.PendienteInstalacion);
        var openTickets = tickets.Where(t => OpenTicketStatuses.Contains(t.Status));
        var openOrders = orders.Where(o => OpenOrderStatuses.Contains(o.Status));

        var home = new ClientHomeDto
        {
            ClientName = await _db.Clients.Where(c => c.Id == clientId).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken) ?? string.Empty,
            LocationCount = await locations.CountAsync(cancellationToken),
            OpenTickets = await openTickets.CountAsync(cancellationToken),
            TicketsInProgress = await openTickets.CountAsync(t => t.Status == ServiceTicketStatus.EnProceso, cancellationToken),
            OpenMaintenance = await openOrders.CountAsync(cancellationToken),
            MaintenanceScheduled = await openOrders.CountAsync(o => o.PlannedAt != null && o.Status != MaintenanceOrderStatus.EnProceso, cancellationToken),
            MaintenanceInProgress = await openOrders.CountAsync(o => o.Status == MaintenanceOrderStatus.EnProceso, cancellationToken),
            MaintenanceWithoutVisitDate = await openOrders.CountAsync(o => o.PlannedAt == null && o.Status != MaintenanceOrderStatus.EnProceso, cancellationToken),
            PendingInstallations = await pendingInstallations.CountAsync(cancellationToken),
            InstallationsInProgress = await pendingInstallations.CountAsync(
                a => _db.TimeLogs.Any(tl => tl.AssetId == a.Id && tl.EndTime == null), cancellationToken),
            InstalledAssets = await assets.CountAsync(a => a.LifecycleStatus == AssetLifecycleStatus.Instalado, cancellationToken),
            AssetsInMaintenance = await assets.CountAsync(a => a.LifecycleStatus == AssetLifecycleStatus.EnMantenimiento, cancellationToken),
            AssetsWithOpenTicket = await openTickets.Where(t => t.AssetId != null).Select(t => t.AssetId).Distinct().CountAsync(cancellationToken)
        };

        // Primero lo abierto (lo que pide atención), luego lo más reciente.
        home.RecentTickets = await tickets
            .OrderByDescending(t => OpenTicketStatuses.Contains(t.Status)).ThenByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Take(ListSize)
            .Select(t => new ClientHomeTicketDto
            {
                Id = t.Id,
                Equipment = t.Asset != null
                    ? t.Asset.AssetModel.AssetBrand.Name + " " + t.Asset.AssetModel.Name
                    : (t.ExternalAssetBrand != null ? t.ExternalAssetBrand + " " + t.ExternalAssetModel : "Equipo sin catalogar"),
                LocationName = t.ClientLocation.Name,
                Description = t.Description.Length > 120 ? t.Description.Substring(0, 120) + "…" : t.Description,
                Status = t.Status.ToString(),
                Priority = t.Priority.ToString(),
                TechnicianName = t.Technician != null ? t.Technician.User.FullName : null,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Lo que viene primero: la visita ya fijada y, si no la hay, la fecha objetivo del cronograma.
        home.UpcomingMaintenance = await openOrders
            .OrderBy(o => o.PlannedAt ?? o.ScheduledDate).ThenBy(o => o.Id)
            .Take(ListSize)
            .Select(o => new ClientHomeMaintenanceDto
            {
                Id = o.Id,
                Equipment = o.Asset.AssetModel.AssetBrand.Name + " " + o.Asset.AssetModel.Name,
                LocationName = o.Asset.CurrentClientLocation != null ? o.Asset.CurrentClientLocation.Name : null,
                Status = o.Status.ToString(),
                ScheduledDate = o.ScheduledDate,
                PlannedAt = o.PlannedAt,
                TechnicianName = o.Technician != null ? o.Technician.User.FullName : null,
                CommentCount = o.Comments.Count()
            })
            .ToListAsync(cancellationToken);

        home.Installations = await pendingInstallations
            .OrderBy(a => a.CurrentClientLocation!.Name).ThenBy(a => a.Id)
            .Take(ListSize)
            .Select(a => new ClientHomeInstallationDto
            {
                AssetId = a.Id,
                Equipment = a.AssetModel.AssetBrand.Name + " " + a.AssetModel.Name,
                LocationName = a.CurrentClientLocation != null ? a.CurrentClientLocation.Name : null,
                InProgress = _db.TimeLogs.Any(tl => tl.AssetId == a.Id && tl.EndTime == null)
            })
            .ToListAsync(cancellationToken);

        return home;
    }
}
