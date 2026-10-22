using Microsoft.EntityFrameworkCore;
using Toner.Application.Assignment;
using Toner.Application.Common.Paging;
using Toner.Application.Common;
using Toner.Application.Common.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance.Dtos;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Maintenance;

public class MaintenanceOrderService : IMaintenanceOrderService
{
    // Antes de EnProceso todavía se puede (re)asignar a otro técnico sin restricción especial.
    private static readonly MaintenanceOrderStatus[] AssignableStatuses =
    {
        MaintenanceOrderStatus.Pendiente, MaintenanceOrderStatus.Asignada
    };

    // Tope de lectura del hilo: una orden no debería acercarse, pero la consulta no debe poder crecer sin límite.
    private const int MaxCommentsPerOrder = 500;

    private readonly IApplicationDbContext _db;
    private readonly IMaintenanceScheduleEngine _engine;
    private readonly IAssignmentEngine _assignmentEngine;

    public MaintenanceOrderService(IApplicationDbContext db, IMaintenanceScheduleEngine engine, IAssignmentEngine assignmentEngine)
    {
        _db = db;
        _engine = engine;
        _assignmentEngine = assignmentEngine;
    }

    public async Task<MaintenanceOrderDto> CreateManualAsync(
        CreateManualMaintenanceOrderRequest request, Guid requestedByUserId, CancellationToken cancellationToken = default)
    {
        var order = await PrepareManualAsync(request, requestedByUserId, cancellationToken);

        // Mismo patrón que ServiceTicketService.CreateAsync: la asignación corre sobre la entidad sin guardar y
        // todo cae en un único SaveChanges.
        await _assignmentEngine.AssignMaintenanceOrderAsync(order, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(order.Id, cancellationToken);
    }

    // Valida y deja trackeada (Pendiente, sin técnico, sin guardar) la orden a demanda: la comparten el staff
    // (CreateManualAsync, que la asigna por el motor) y el técnico que inicia su propio mantenimiento
    // (TechnicianCheckInService.StartMaintenanceAsync, que se la asigna a sí mismo). Las reglas de "se puede
    // mantener este equipo" viven aquí, una sola vez.
    public async Task<MaintenanceOrder> PrepareManualAsync(
        CreateManualMaintenanceOrderRequest request, Guid requestedByUserId, CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == request.AssetId, cancellationToken)
            ?? throw new NotFoundException(nameof(Asset), request.AssetId);

        if (asset.LifecycleStatus != AssetLifecycleStatus.Instalado)
        {
            throw new ConflictException($"Solo se puede pedir mantenimiento de un activo instalado (estado actual: '{asset.LifecycleStatus}').");
        }

        // Toda orden cuelga de un cronograma (de ahí salen el cliente y el contrato): un equipo instalado sin
        // cronograma activo no está bajo contrato.
        var schedule = await _db.MaintenanceSchedules
            .FirstOrDefaultAsync(s => s.AssetId == asset.Id && s.IsActive, cancellationToken)
            ?? throw new ConflictException("Este activo no tiene un cronograma de mantenimiento activo (no está bajo un contrato vigente).");

        // Una sola orden abierta por equipo: es la misma condición con la que el motor del cronograma se salta un
        // equipo, así que mientras esta orden siga abierta el job no genera otra encima.
        var hasOpenOrder = await _db.MaintenanceOrders.AnyAsync(
            o => o.AssetId == asset.Id
                && (o.Status == MaintenanceOrderStatus.Pendiente || o.Status == MaintenanceOrderStatus.Asignada || o.Status == MaintenanceOrderStatus.EnProceso),
            cancellationToken);
        if (hasOpenOrder)
        {
            throw new ConflictException("Este activo ya tiene una orden de mantenimiento abierta. Complétala o cancélala antes de pedir otra.");
        }

        var order = new MaintenanceOrder
        {
            MaintenanceScheduleId = schedule.Id,
            AssetId = asset.Id,
            ClientId = schedule.ClientId,
            Status = MaintenanceOrderStatus.Pendiente,
            ScheduledDate = DateTime.UtcNow,
            IncludesGeneral = request.IncludesGeneral,
            IncludesUnits = request.IncludesUnits,
            IncludesConsumables = request.IncludesConsumables,
            IsManual = true,
            RequestedByUserId = requestedByUserId,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
        };
        _db.MaintenanceOrders.Add(order);

        return order;
    }

    public async Task<PagedResult<MaintenanceOrderDto>> ListAsync(
        RequestingUser requestingUser, int? page, int? pageSize, MaintenanceOrderListFilter? filter = null, CancellationToken cancellationToken = default)
    {
        // Sobre la ENTIDAD, antes de proyectar: el DTO expone Status como texto y EF no traduce comparaciones sobre eso.
        var query = _db.MaintenanceOrders.AsQueryable();

        if (requestingUser.IsTechnician)
        {
            query = query.Where(o => o.TechnicianId == requestingUser.TechnicianId);
        }
        else if (requestingUser.IsClient)
        {
            var clientId = requestingUser.RequireClientId();
            query = query.Where(o => o.ClientId == clientId);

            var allowedLocations = await _db.AllowedLocationIdsAsync(requestingUser, cancellationToken);
            if (allowedLocations is not null)
            {
                query = query.Where(o => o.Asset.CurrentClientLocationId != null
                    && allowedLocations.Contains(o.Asset.CurrentClientLocationId.Value));
            }
        }

        // Filtros del servidor (AUDIT.md H-11): DESPUÉS del alcance del rol, así que son un AND.
        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var status = EnumParsing.ParseOrThrow<MaintenanceOrderStatus>(filter.Status, nameof(filter.Status));
                query = query.Where(o => o.Status == status);
            }

            if (filter.ClientId is { } clientId)
            {
                query = query.Where(o => o.ClientId == clientId);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim().ToLower();
                query = query.Where(o =>
                    o.Asset.SerialNumber.ToLower().Contains(term)
                    || o.Asset.AssetModel.Name.ToLower().Contains(term)
                    || o.Asset.AssetModel.AssetBrand.Name.ToLower().Contains(term));
            }
        }

        // Desempate por Id: sin él, paginar por offset con CreatedAt repetido repite o salta filas.
        var result = await ProjectedFrom(query.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id))
            .ToOffsetPageAsync(page, pageSize, cancellationToken);

        if (requestingUser.IsClient)
        {
            foreach (var item in result.Items)
            {
                HideInternalFields(item);
            }
        }

        return result;
    }

    // Lo que el cliente nunca ve de una orden: el motivo que el staff dio al pedirla (nota interna).
    private static void HideInternalFields(MaintenanceOrderDto order) => order.Reason = null;

    public async Task<PagedResult<MaintenanceOrderDto>> ListByScheduleAsync(Guid scheduleId, string? cursor, int? pageSize, CancellationToken cancellationToken = default)
    {
        var query = Projected(_db).Where(o => o.MaintenanceScheduleId == scheduleId);

        var position = PageCursor.Decode(cursor);
        if (position is not null)
        {
            var (ts, lastId) = position.Value;
            query = query.Where(o => o.CreatedAt < ts || (o.CreatedAt == ts && o.Id.CompareTo(lastId) < 0));
        }

        return await query
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .ToCursorPageAsync(pageSize, last => PageCursor.Encode(last.CreatedAt, last.Id), cancellationToken);
    }

    public async Task<PagedResult<MaintenanceOrderDto>> ListInCoverageAsync(Guid technicianId, int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        var coveredCityIds = await _db.CoveredCityIds(technicianId).ToListAsync(cancellationToken);

        if (coveredCityIds.Count == 0)
        {
            return PagedResultFactory.Empty<MaintenanceOrderDto>(pageSize);
        }

        var activeStatuses = new[]
        {
            MaintenanceOrderStatus.Pendiente, MaintenanceOrderStatus.Asignada, MaintenanceOrderStatus.EnProceso
        };

        var query = _db.MaintenanceOrders.Where(o =>
            activeStatuses.Contains(o.Status) &&
            o.TechnicianId != technicianId &&
            o.Asset.CurrentClientLocation != null &&
            coveredCityIds.Contains(o.Asset.CurrentClientLocation.CityId));

        return await ProjectedFrom(query).OrderByDescending(o => o.CreatedAt).ToOffsetPageAsync(page, pageSize, cancellationToken);
    }

    public async Task<MaintenanceOrderDto> GetByIdAsync(RequestingUser requestingUser, Guid id, CancellationToken cancellationToken = default)
    {
        var order = await Projected(_db).FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(MaintenanceOrder), id);

        if (requestingUser.IsTechnician && order.TechnicianId != requestingUser.TechnicianId)
        {
            throw new ForbiddenException("Esta orden no está asignada a ti.");
        }

        if (requestingUser.IsClient)
        {
            var clientId = requestingUser.RequireClientId();
            var visible = _db.MaintenanceOrders.Where(o => o.Id == id && o.ClientId == clientId);
            var allowedLocations = await _db.AllowedLocationIdsAsync(requestingUser, cancellationToken);
            if (allowedLocations is not null)
            {
                visible = visible.Where(o => o.Asset.CurrentClientLocationId != null
                    && allowedLocations.Contains(o.Asset.CurrentClientLocationId.Value));
            }

            if (!await visible.AnyAsync(cancellationToken))
            {
                throw new ForbiddenException("No puedes ver una orden que no pertenece a tu cliente.");
            }

            HideInternalFields(order);
        }

        return order;
    }

    public async Task<MaintenanceOrderDto> ScheduleAsync(
        Guid id, Guid technicianId, Guid authorUserId, DateTime plannedAtUtc, CancellationToken cancellationToken = default)
    {
        var order = await _db.MaintenanceOrders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(MaintenanceOrder), id);

        if (order.TechnicianId != technicianId)
        {
            throw new ForbiddenException("Esta orden no está asignada a ti.");
        }

        if (order.Status != MaintenanceOrderStatus.Asignada)
        {
            throw new ConflictException($"Solo se puede programar una orden asignada que aún no se inicia (estado actual: '{order.Status}').");
        }

        ApplySchedule(order, authorUserId, plannedAtUtc);

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    public void ApplySchedule(MaintenanceOrder order, Guid authorUserId, DateTime plannedAtUtc)
    {
        var planned = DateTime.SpecifyKind(plannedAtUtc.ToUniversalTime(), DateTimeKind.Utc);
        order.PlannedAt = planned;
        order.LastCommentAt = DateTime.UtcNow;

        _db.MaintenanceOrderComments.Add(new MaintenanceOrderComment
        {
            MaintenanceOrderId = order.Id,
            ClientId = order.ClientId,
            AuthorUserId = authorUserId,
            AuthorRole = RoleNames.Tecnico,
            Kind = OrderCommentKind.Programacion,
            PlannedAt = planned
        });
    }

    public async Task<IReadOnlyList<MaintenanceOrderCommentDto>> ListCommentsAsync(
        RequestingUser requestingUser, Guid id, CancellationToken cancellationToken = default)
    {
        // Mismo alcance que ver la orden (rol, empresa y sedes): lanza NotFound/Forbidden si no corresponde.
        await GetByIdAsync(requestingUser, id, cancellationToken);

        return await _db.MaintenanceOrderComments
            .Where(c => c.MaintenanceOrderId == id)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Take(MaxCommentsPerOrder)
            .Select(c => new MaintenanceOrderCommentDto
            {
                Id = c.Id,
                MaintenanceOrderId = c.MaintenanceOrderId,
                Kind = c.Kind.ToString(),
                Body = c.Body,
                PlannedAt = c.PlannedAt,
                AuthorUserId = c.AuthorUserId,
                AuthorName = c.AuthorUser.FullName,
                AuthorRole = c.AuthorRole,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<MaintenanceOrderCommentDto> AddCommentAsync(
        RequestingUser requestingUser, Guid id, string body, CancellationToken cancellationToken = default)
    {
        await GetByIdAsync(requestingUser, id, cancellationToken);

        var order = await _db.MaintenanceOrders.FirstAsync(o => o.Id == id, cancellationToken);
        if (order.Status is MaintenanceOrderStatus.Completada or MaintenanceOrderStatus.Cancelada)
        {
            throw new ConflictException("La orden ya está cerrada: no admite más comentarios.");
        }

        var comment = new MaintenanceOrderComment
        {
            MaintenanceOrderId = order.Id,
            ClientId = order.ClientId,
            AuthorUserId = requestingUser.UserId,
            AuthorRole = requestingUser.Role,
            Kind = OrderCommentKind.Comentario,
            Body = body.Trim()
        };
        _db.MaintenanceOrderComments.Add(comment);
        // Tocar la orden en el mismo guardado dispara el aviso en vivo de "MaintenanceOrder" (cliente, técnico y staff).
        order.LastCommentAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var authorName = await _db.Users.Where(u => u.Id == requestingUser.UserId).Select(u => u.FullName).FirstAsync(cancellationToken);
        return new MaintenanceOrderCommentDto
        {
            Id = comment.Id,
            MaintenanceOrderId = comment.MaintenanceOrderId,
            Kind = comment.Kind.ToString(),
            Body = comment.Body,
            AuthorUserId = comment.AuthorUserId,
            AuthorName = authorName,
            AuthorRole = comment.AuthorRole,
            CreatedAt = comment.CreatedAt
        };
    }

    public async Task<MaintenanceOrderDto> AssignAsync(Guid id, AssignMaintenanceOrderRequest request, Guid assignedByUserId, CancellationToken cancellationToken = default)
    {
        var order = await _db.MaintenanceOrders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(MaintenanceOrder), id);

        if (!AssignableStatuses.Contains(order.Status))
        {
            throw new ConflictException($"No se puede asignar una orden en estado '{order.Status}'.");
        }

        order.TechnicianId = request.TechnicianId;
        order.Status = MaintenanceOrderStatus.Asignada;
        order.PlannedAt = null; // el plan era del técnico anterior

        _db.AssignmentHistories.Add(new AssignmentHistory
        {
            MaintenanceOrderId = order.Id,
            TechnicianId = request.TechnicianId,
            AssignedByUserId = assignedByUserId,
            AssignmentType = AssignmentType.Manual,
            Reason = request.Reason?.Trim()
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    public async Task<MaintenanceOrderDto> ClaimAsync(Guid id, Guid technicianId, CancellationToken cancellationToken = default)
    {
        var order = await _db.MaintenanceOrders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(MaintenanceOrder), id);

        if (!AssignableStatuses.Contains(order.Status))
        {
            throw new ConflictException($"No se puede tomar una orden en estado '{order.Status}'.");
        }

        if (order.TechnicianId == technicianId)
        {
            throw new ConflictException("Esta orden ya está asignada a ti.");
        }

        var claimingUserId = await _db.Technicians
            .Where(t => t.Id == technicianId)
            .Select(t => t.UserId)
            .FirstAsync(cancellationToken);

        order.TechnicianId = technicianId;
        order.Status = MaintenanceOrderStatus.Asignada;
        order.PlannedAt = null; // el plan era del técnico anterior

        _db.AssignmentHistories.Add(new AssignmentHistory
        {
            MaintenanceOrderId = order.Id,
            TechnicianId = technicianId,
            AssignedByUserId = claimingUserId,
            AssignmentType = AssignmentType.Reclamada
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    public async Task<MaintenanceOrderDto> CompleteAsync(
        Guid id,
        CompleteMaintenanceOrderRequest request,
        Guid completedByUserId,
        CancellationToken cancellationToken = default)
    {
        await PrepareCompleteAsync(id, request, completedByUserId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    // Igual que CompleteAsync pero sin guardar — ver IMaintenanceOrderService.
    public async Task<MaintenanceOrder> PrepareCompleteAsync(
        Guid id,
        CompleteMaintenanceOrderRequest request,
        Guid completedByUserId,
        CancellationToken cancellationToken = default)
    {
        var order = await _db.MaintenanceOrders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(MaintenanceOrder), id);

        if (order.Status is MaintenanceOrderStatus.Completada or MaintenanceOrderStatus.Cancelada)
        {
            throw new ConflictException($"La orden ya está en estado '{order.Status}' y no se puede completar.");
        }

        var lastReading = await _db.MeterReadings
            .Where(m => m.AssetId == order.AssetId)
            .OrderByDescending(m => m.ReadingDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (lastReading is not null && request.CounterValue < lastReading.CounterValue)
        {
            throw new ConflictException(
                $"La lectura ({request.CounterValue}) no puede ser menor a la última registrada ({lastReading.CounterValue}).");
        }

        var now = DateTime.UtcNow;
        var readingDate = request.ReadingDate ?? now;

        order.Status = MaintenanceOrderStatus.Completada;
        order.CompletedAt = now;

        _db.MeterReadings.Add(new MeterReading
        {
            AssetId = order.AssetId,
            // Captura al escribir desde la orden, que ya lleva el cliente denormalizado (fase 3b).
            ClientId = order.ClientId,
            ReadingDate = readingDate,
            CounterValue = request.CounterValue,
            RegisteredByUserId = completedByUserId
        });

        await _engine.RecalculateAfterMaintenanceAsync(
            order.MaintenanceScheduleId, request.CounterValue, readingDate,
            order.IncludesGeneral, order.IncludesUnits, order.IncludesConsumables, cancellationToken);

        return order;
    }

    public async Task<MaintenanceOrderDto> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _db.MaintenanceOrders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(MaintenanceOrder), id);

        if (order.Status is MaintenanceOrderStatus.Completada or MaintenanceOrderStatus.Cancelada)
        {
            throw new ConflictException($"La orden ya está en estado '{order.Status}' y no se puede cancelar.");
        }

        order.Status = MaintenanceOrderStatus.Cancelada;
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    // El técnico se arrepiente de un mantenimiento que inició él mismo y todavía no empezó (sin check-in): lo
    // cancela y el equipo vuelve a quedar libre para el cronograma. Sin esta salida, una orden abandonada
    // bloquearía al motor del cronograma para ese equipo (salta los que ya tienen una orden abierta). Solo las
    // que él creó y solo mientras estén Asignada: una EnProceso tiene un check-in abierto que cerrar, y las
    // del cronograma o del staff las cancela el staff.
    public async Task<MaintenanceOrderDto> CancelSelfStartedAsync(Guid id, Guid technicianId, CancellationToken cancellationToken = default)
    {
        var order = await _db.MaintenanceOrders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(MaintenanceOrder), id);

        var technicianUserId = await _db.Technicians
            .Where(t => t.Id == technicianId)
            .Select(t => t.UserId)
            .FirstAsync(cancellationToken);

        if (order.TechnicianId != technicianId || !order.IsManual || order.RequestedByUserId != technicianUserId)
        {
            throw new ForbiddenException("Solo puedes cancelar un mantenimiento que iniciaste tú.");
        }

        if (order.Status != MaintenanceOrderStatus.Asignada)
        {
            throw new ConflictException($"No puedes cancelar una orden en estado '{order.Status}'.");
        }

        order.Status = MaintenanceOrderStatus.Cancelada;
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    public async Task<PagedResult<AssignmentHistoryDto>> GetAssignmentHistoryAsync(Guid id, string? cursor, int? pageSize, CancellationToken cancellationToken = default)
    {
        var orderExists = await _db.MaintenanceOrders.AnyAsync(o => o.Id == id, cancellationToken);
        if (!orderExists)
        {
            throw new NotFoundException(nameof(MaintenanceOrder), id);
        }

        var query = _db.AssignmentHistories.Where(a => a.MaintenanceOrderId == id);

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

    private async Task<MaintenanceOrderDto> ToDtoAsync(Guid id, CancellationToken cancellationToken) =>
        await Projected(_db).FirstAsync(o => o.Id == id, cancellationToken);

    private static IQueryable<MaintenanceOrderDto> Projected(IApplicationDbContext db) => ProjectedFrom(db.MaintenanceOrders);

    private static IQueryable<MaintenanceOrderDto> ProjectedFrom(IQueryable<MaintenanceOrder> query) =>
        query.Select(o => new MaintenanceOrderDto
        {
            Id = o.Id,
            MaintenanceScheduleId = o.MaintenanceScheduleId,
            AssetId = o.AssetId,
            AssetBrandName = o.Asset.AssetModel.AssetBrand.Name,
            AssetModel = o.Asset.AssetModel.Name,
            AssetSerialNumber = o.Asset.SerialNumber,
            Status = o.Status.ToString(),
            TechnicianId = o.TechnicianId,
            TechnicianName = o.Technician != null ? o.Technician.User.FullName : null,
            ClientLocationName = o.Asset.CurrentClientLocation != null ? o.Asset.CurrentClientLocation.Name : null,
            CityName = o.Asset.CurrentClientLocation != null ? o.Asset.CurrentClientLocation.City.Name : null,
            IncludesGeneral = o.IncludesGeneral,
            IncludesUnits = o.IncludesUnits,
            IncludesConsumables = o.IncludesConsumables,
            IsManual = o.IsManual,
            IsSelfStarted = o.IsManual && o.Technician != null && o.RequestedByUserId == o.Technician.UserId,
            Reason = o.Reason,
            ScheduledDate = o.ScheduledDate,
            PlannedAt = o.PlannedAt,
            LastCommentAt = o.LastCommentAt,
            CommentCount = o.Comments.Count(),
            CompletedAt = o.CompletedAt,
            CreatedAt = o.CreatedAt
        });
}
