using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Paging;
using Toner.Application.Assignment;
using Toner.Application.Common;
using Toner.Application.Common.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Tickets.Dtos;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tickets;

public class ServiceTicketService : IServiceTicketService
{
    // Asignado solo se alcanza vía AssignAsync (que además crea el AssignmentHistory), nunca por SetStatusAsync.
    private static readonly Dictionary<ServiceTicketStatus, ServiceTicketStatus[]> AllowedTransitions = new()
    {
        [ServiceTicketStatus.Abierto] = new[] { ServiceTicketStatus.Cancelado },
        [ServiceTicketStatus.SinAsignar] = new[] { ServiceTicketStatus.Cancelado },
        [ServiceTicketStatus.Asignado] = new[] { ServiceTicketStatus.EnProceso, ServiceTicketStatus.Cancelado },
        [ServiceTicketStatus.EnProceso] = new[] { ServiceTicketStatus.Resuelto, ServiceTicketStatus.Cancelado },
        [ServiceTicketStatus.Resuelto] = new[] { ServiceTicketStatus.Cerrado, ServiceTicketStatus.EnProceso },
        [ServiceTicketStatus.Cerrado] = Array.Empty<ServiceTicketStatus>(),
        [ServiceTicketStatus.Cancelado] = Array.Empty<ServiceTicketStatus>()
    };

    // Antes de EnProceso todavía se puede reasignar a otro técnico sin restricción especial.
    private static readonly ServiceTicketStatus[] AssignableStatuses =
    {
        ServiceTicketStatus.Abierto, ServiceTicketStatus.SinAsignar, ServiceTicketStatus.Asignado
    };

    private readonly IApplicationDbContext _db;
    private readonly IAssignmentEngine _assignmentEngine;

    public ServiceTicketService(IApplicationDbContext db, IAssignmentEngine assignmentEngine)
    {
        _db = db;
        _assignmentEngine = assignmentEngine;
    }

    public async Task<ServiceTicketDto> CreateAsync(RequestingUser requestingUser, CreateServiceTicketRequest request, CancellationToken cancellationToken = default)
    {
        // La validación del payload va ANTES de tocar la base: es más barata, y sobre todo un
        // Priority inválido tiene que salir como ValidationException → 400 (CODE_QUALITY_AUDIT.md
        // hallazgo #20). Si la consulta de la sede corriera primero, una petición con Priority
        // inválido devolvería el error de esa consulta y no el de validación.
        var priority = string.IsNullOrEmpty(request.Priority)
            ? ServiceTicketPriority.Media
            : EnumParsing.ParseOrThrow<ServiceTicketPriority>(request.Priority, nameof(request.Priority));

        // La consulta ahora es incondicional (antes solo corría para no-staff): su resultado también
        // alimenta ServiceTicket.ClientId, la columna denormalizada que usa la política RLS. Sigue
        // siendo una sola consulta, que sirve para el chequeo de autorización y para la captura.
        var locationClientId = await _db.ClientLocations
            .Where(l => l.Id == request.ClientLocationId)
            .Select(l => l.ClientId)
            .FirstAsync(cancellationToken);

        if (!requestingUser.IsStaff && locationClientId != requestingUser.RequireClientId())
        {
            throw new ForbiddenException("No puedes reportar tickets para una sede que no pertenece a tu cliente.");
        }

        var ticket = new ServiceTicket
        {
            ClientLocationId = request.ClientLocationId,
            // Captura al escribir: el ticket queda atado al cliente dueño de la sede en este momento
            // (ver ServiceTicket.ClientId). ClientLocation.ClientId es inmutable, así que no puede
            // desincronizarse después.
            ClientId = locationClientId,
            AssetId = request.AssetId,
            ReportedByUserId = requestingUser.UserId,
            Description = request.Description.Trim(),
            Status = ServiceTicketStatus.Abierto,
            Priority = priority
        };

        _db.ServiceTickets.Add(ticket);

        // Intento de asignación automática por cobertura/carga apenas se crea el ticket; si no hay
        // candidato, el motor mismo deja el ticket en SinAsignar y registra el intento fallido.
        // Corre ANTES del SaveChanges y sobre la entidad todavía sin guardar: el ticket y su
        // asignación (o su AssignmentHistory de intento fallido) se persisten en un único commit.
        // Antes eran dos, y si el segundo fallaba el ticket quedaba en Abierto sin que nada volviera
        // a intentar asignarlo (CODE_QUALITY_AUDIT.md hallazgo #8).
        await _assignmentEngine.AssignServiceTicketAsync(ticket, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(ticket.Id, cancellationToken);
    }

    public async Task<PagedResult<ServiceTicketDto>> ListAsync(RequestingUser requestingUser, int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        var query = Projected(_db);

        if (requestingUser.IsTechnician)
        {
            query = query.Where(t => t.TechnicianId == requestingUser.TechnicianId);
        }
        else if (!requestingUser.IsStaff)
        {
            var clientId = requestingUser.RequireClientId();
            query = query.Where(t => t.ClientId == clientId);
        }

        return await query.OrderByDescending(t => t.CreatedAt).ToOffsetPageAsync(page, pageSize, cancellationToken);
    }

    public async Task<PagedResult<ServiceTicketDto>> ListInCoverageAsync(Guid technicianId, int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        var coveredCityIds = await _db.CoveredCityIds(technicianId).ToListAsync(cancellationToken);

        if (coveredCityIds.Count == 0)
        {
            return PagedResultFactory.Empty<ServiceTicketDto>(pageSize);
        }

        var activeStatuses = new[]
        {
            ServiceTicketStatus.Abierto, ServiceTicketStatus.SinAsignar, ServiceTicketStatus.Asignado, ServiceTicketStatus.EnProceso
        };

        var query = _db.ServiceTickets.Where(t =>
            activeStatuses.Contains(t.Status) &&
            t.TechnicianId != technicianId &&
            coveredCityIds.Contains(t.ClientLocation.CityId));

        return await ProjectedFrom(query).OrderByDescending(t => t.CreatedAt).ToOffsetPageAsync(page, pageSize, cancellationToken);
    }

    public async Task<ServiceTicketDto> GetByIdAsync(RequestingUser requestingUser, Guid id, CancellationToken cancellationToken = default)
    {
        var ticket = await Projected(_db).FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(ServiceTicket), id);

        if (requestingUser.IsTechnician && ticket.TechnicianId != requestingUser.TechnicianId)
        {
            throw new ForbiddenException("Este ticket no está asignado a ti.");
        }

        if (!requestingUser.IsStaff && !requestingUser.IsTechnician && ticket.ClientId != requestingUser.RequireClientId())
        {
            throw new ForbiddenException("No puedes ver un ticket que no pertenece a tu cliente.");
        }

        // Notas internas del técnico: solo para staff y el técnico asignado, nunca para el cliente. Se
        // resuelven aquí y no en la proyección compartida para no cargar una subconsulta en cada listado.
        if (ticket.ResolvedAt != null && (requestingUser.IsStaff || requestingUser.IsTechnician))
        {
            var closedLogs = await _db.TimeLogs
                .Where(l => l.ServiceTicketId == id && l.EndTime != null)
                .OrderBy(l => l.EndTime)
                .Select(l => new { l.StartTime, l.EndTime, l.Notes })
                .ToListAsync(cancellationToken);

            if (closedLogs.Count > 0)
            {
                // Suma de todas las visitas cerradas: un ticket con pausas tiene más de un TimeLog.
                ticket.ResolutionDurationMinutes = (int)Math.Round(
                    closedLogs.Sum(l => (l.EndTime!.Value - l.StartTime).TotalMinutes));
                ticket.ResolutionNotes = closedLogs
                    .Select(l => l.Notes)
                    .LastOrDefault(n => !string.IsNullOrWhiteSpace(n));
            }
        }

        return ticket;
    }

    public async Task<ServiceTicketDto> AssignAsync(Guid id, AssignTicketRequest request, Guid assignedByUserId, CancellationToken cancellationToken = default)
    {
        var ticket = await _db.ServiceTickets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(ServiceTicket), id);

        if (!AssignableStatuses.Contains(ticket.Status))
        {
            throw new ConflictException($"No se puede asignar un ticket en estado '{ticket.Status}'.");
        }

        ticket.TechnicianId = request.TechnicianId;
        ticket.Status = ServiceTicketStatus.Asignado;

        _db.AssignmentHistories.Add(new AssignmentHistory
        {
            ServiceTicketId = ticket.Id,
            ClientId = ticket.ClientId,
            TechnicianId = request.TechnicianId,
            AssignedByUserId = assignedByUserId,
            AssignmentType = AssignmentType.Manual,
            Reason = request.Reason?.Trim()
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    public async Task<ServiceTicketDto> ClaimAsync(Guid id, Guid technicianId, CancellationToken cancellationToken = default)
    {
        var ticket = await _db.ServiceTickets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(ServiceTicket), id);

        if (!AssignableStatuses.Contains(ticket.Status))
        {
            throw new ConflictException($"No se puede tomar un ticket en estado '{ticket.Status}'.");
        }

        if (ticket.TechnicianId == technicianId)
        {
            throw new ConflictException("Este ticket ya está asignado a ti.");
        }

        var claimingUserId = await _db.Technicians
            .Where(t => t.Id == technicianId)
            .Select(t => t.UserId)
            .FirstAsync(cancellationToken);

        ticket.TechnicianId = technicianId;
        ticket.Status = ServiceTicketStatus.Asignado;

        _db.AssignmentHistories.Add(new AssignmentHistory
        {
            ServiceTicketId = ticket.Id,
            ClientId = ticket.ClientId,
            TechnicianId = technicianId,
            AssignedByUserId = claimingUserId,
            AssignmentType = AssignmentType.Reclamada
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    public async Task<ServiceTicketDto> SetStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        await PrepareStatusChangeAsync(id, status, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    // Igual que SetStatusAsync pero sin guardar — ver IServiceTicketService.
    public async Task<ServiceTicket> PrepareStatusChangeAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        var ticket = await _db.ServiceTickets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(ServiceTicket), id);

        var newStatus = EnumParsing.ParseOrThrow<ServiceTicketStatus>(status, "Status");

        if (!AllowedTransitions[ticket.Status].Contains(newStatus))
        {
            throw new ConflictException($"No se puede pasar de '{ticket.Status}' a '{newStatus}'.");
        }

        ticket.Status = newStatus;
        ticket.ResolvedAt = newStatus == ServiceTicketStatus.Resuelto ? DateTime.UtcNow : ticket.ResolvedAt;
        ticket.ClosedAt = newStatus == ServiceTicketStatus.Cerrado ? DateTime.UtcNow : ticket.ClosedAt;

        return ticket;
    }

    public async Task<PagedResult<AssignmentHistoryDto>> GetAssignmentHistoryAsync(Guid id, string? cursor, int? pageSize, CancellationToken cancellationToken = default)
    {
        var ticketExists = await _db.ServiceTickets.AnyAsync(t => t.Id == id, cancellationToken);
        if (!ticketExists)
        {
            throw new NotFoundException(nameof(ServiceTicket), id);
        }

        var query = _db.AssignmentHistories.Where(a => a.ServiceTicketId == id);

        var position = PageCursor.Decode(cursor);
        if (position is not null)
        {
            var (ts, lastId) = position.Value;
            query = query.Where(a => a.AssignedAt < ts || (a.AssignedAt == ts && a.Id.CompareTo(lastId) < 0));
        }

        return await query
            .OrderByDescending(a => a.AssignedAt).ThenByDescending(a => a.Id)
            .Select(a => new AssignmentHistoryDto
            {
                Id = a.Id,
                TechnicianId = a.TechnicianId,
                TechnicianName = a.Technician != null ? a.Technician.User.FullName : null,
                AssignedByUserName = a.AssignedByUser != null ? a.AssignedByUser.FullName : null,
                AssignmentType = a.AssignmentType.ToString(),
                Reason = a.Reason,
                AssignedAt = a.AssignedAt
            })
            .ToCursorPageAsync(pageSize, last => PageCursor.Encode(last.AssignedAt, last.Id), cancellationToken);
    }

    private async Task<ServiceTicketDto> ToDtoAsync(Guid id, CancellationToken cancellationToken) =>
        await Projected(_db).FirstAsync(t => t.Id == id, cancellationToken);

    private static IQueryable<ServiceTicketDto> Projected(IApplicationDbContext db) => ProjectedFrom(db.ServiceTickets);

    private static IQueryable<ServiceTicketDto> ProjectedFrom(IQueryable<ServiceTicket> query) =>
        query.Select(t => new ServiceTicketDto
        {
            Id = t.Id,
            ClientLocationId = t.ClientLocationId,
            ClientLocationName = t.ClientLocation.Name,
            ClientId = t.ClientLocation.ClientId,
            ClientName = t.ClientLocation.Client.Name,
            CityName = t.ClientLocation.City.Name,
            AssetId = t.AssetId,
            AssetBrandName = t.Asset != null ? t.Asset.AssetModel.AssetBrand.Name : null,
            AssetModel = t.Asset != null ? t.Asset.AssetModel.Name : null,
            AssetSerialNumber = t.Asset != null ? t.Asset.SerialNumber : null,
            AssetUnderContract = t.Asset != null
                && t.Asset.ContractAssets.Any(ca => ca.EndDate == null && ca.Contract.Status == ContractStatus.Activo),
            ExternalAssetBrand = t.ExternalAssetBrand,
            ExternalAssetModel = t.ExternalAssetModel,
            ExternalAssetCounter = t.ExternalAssetCounter,
            ReportedByUserId = t.ReportedByUserId,
            ReportedByUserName = t.ReportedByUser.FullName,
            Description = t.Description,
            Status = t.Status.ToString(),
            Priority = t.Priority.ToString(),
            TechnicianId = t.TechnicianId,
            TechnicianName = t.Technician != null ? t.Technician.User.FullName : null,
            ResolvedAt = t.ResolvedAt,
            ClosedAt = t.ClosedAt,
            CreatedAt = t.CreatedAt
        });
}
