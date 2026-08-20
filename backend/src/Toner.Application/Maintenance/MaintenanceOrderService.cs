using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Paging;
using Toner.Application.Common;
using Toner.Application.Common.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance.Dtos;
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

    private readonly IApplicationDbContext _db;
    private readonly IMaintenanceScheduleEngine _engine;

    public MaintenanceOrderService(IApplicationDbContext db, IMaintenanceScheduleEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    public async Task<PagedResult<MaintenanceOrderDto>> ListAsync(RequestingUser requestingUser, int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        var query = Projected(_db);

        if (requestingUser.IsTechnician)
        {
            query = query.Where(o => o.TechnicianId == requestingUser.TechnicianId);
        }

        return await query.OrderByDescending(o => o.CreatedAt).ToOffsetPageAsync(page, pageSize, cancellationToken);
    }

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
        var coveredCityIds = await _db.TechnicianCoverages
            .Where(c => c.TechnicianId == technicianId)
            .Select(c => c.CityId)
            .ToListAsync(cancellationToken);

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

        return order;
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
            ScheduledDate = o.ScheduledDate,
            CompletedAt = o.CompletedAt,
            CreatedAt = o.CreatedAt
        });
}
