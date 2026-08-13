using Microsoft.EntityFrameworkCore;
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

    public MaintenanceOrderService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MaintenanceOrderDto>> ListAsync(RequestingUser requestingUser, CancellationToken cancellationToken = default)
    {
        var query = Projected(_db);

        if (requestingUser.IsTechnician)
        {
            query = query.Where(o => o.TechnicianId == requestingUser.TechnicianId);
        }

        return await query.OrderByDescending(o => o.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MaintenanceOrderDto>> ListByScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        return await Projected(_db)
            .Where(o => o.MaintenanceScheduleId == scheduleId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
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

    public async Task<MaintenanceOrderDto> CompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _db.MaintenanceOrders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(MaintenanceOrder), id);

        if (order.Status is MaintenanceOrderStatus.Completada or MaintenanceOrderStatus.Cancelada)
        {
            throw new ConflictException($"La orden ya está en estado '{order.Status}' y no se puede completar.");
        }

        var schedule = await _db.MaintenanceSchedules.FirstAsync(s => s.Id == order.MaintenanceScheduleId, cancellationToken);

        var now = DateTime.UtcNow;
        order.Status = MaintenanceOrderStatus.Completada;
        order.CompletedAt = now;

        schedule.LastExecutedAt = now;

        if (schedule.FrequencyType == MaintenanceFrequencyType.PorContador)
        {
            var lastReading = await _db.MeterReadings
                .Where(m => m.AssetId == schedule.AssetId)
                .OrderByDescending(m => m.ReadingDate)
                .FirstOrDefaultAsync(cancellationToken);

            schedule.LastExecutedCounter = lastReading?.CounterValue;
            schedule.NextDueCounter = (schedule.LastExecutedCounter ?? 0) + schedule.PrintThreshold!.Value;
        }
        else
        {
            schedule.NextDueAt = now.AddDays(schedule.TimeIntervalDays!.Value);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
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

    public async Task<IReadOnlyList<AssignmentHistoryDto>> GetAssignmentHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var orderExists = await _db.MaintenanceOrders.AnyAsync(o => o.Id == id, cancellationToken);
        if (!orderExists)
        {
            throw new NotFoundException(nameof(MaintenanceOrder), id);
        }

        return await _db.AssignmentHistories
            .Include(a => a.Technician!).ThenInclude(t => t.User)
            .Include(a => a.AssignedByUser)
            .Where(a => a.MaintenanceOrderId == id)
            .OrderByDescending(a => a.AssignedAt)
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
            .ToListAsync(cancellationToken);
    }

    private async Task<MaintenanceOrderDto> ToDtoAsync(Guid id, CancellationToken cancellationToken) =>
        await Projected(_db).FirstAsync(o => o.Id == id, cancellationToken);

    private static IQueryable<MaintenanceOrderDto> Projected(IApplicationDbContext db) =>
        db.MaintenanceOrders.Select(o => new MaintenanceOrderDto
        {
            Id = o.Id,
            MaintenanceScheduleId = o.MaintenanceScheduleId,
            AssetId = o.AssetId,
            AssetBrandName = o.Asset.AssetBrand.Name,
            AssetModel = o.Asset.Model,
            AssetSerialNumber = o.Asset.SerialNumber,
            Status = o.Status.ToString(),
            TechnicianId = o.TechnicianId,
            TechnicianName = o.Technician != null ? o.Technician.User.FullName : null,
            ScheduledDate = o.ScheduledDate,
            CompletedAt = o.CompletedAt,
            CreatedAt = o.CreatedAt
        });
}
