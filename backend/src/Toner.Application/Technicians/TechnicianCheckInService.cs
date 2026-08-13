using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Tickets;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Technicians;

public class TechnicianCheckInService : ITechnicianCheckInService
{
    private readonly IApplicationDbContext _db;
    private readonly IServiceTicketService _ticketService;
    private readonly IMaintenanceOrderService _orderService;

    public TechnicianCheckInService(IApplicationDbContext db, IServiceTicketService ticketService, IMaintenanceOrderService orderService)
    {
        _db = db;
        _ticketService = ticketService;
        _orderService = orderService;
    }

    public async Task<TechnicianSelfStatusDto> GetMyStatusAsync(Guid technicianId, CancellationToken cancellationToken = default)
    {
        var technician = await _db.Technicians.FirstOrDefaultAsync(t => t.Id == technicianId, cancellationToken)
            ?? throw new NotFoundException(nameof(Technician), technicianId);

        var openLog = await _db.TimeLogs
            .Where(tl => tl.TechnicianId == technicianId && tl.EndTime == null)
            .OrderByDescending(tl => tl.StartTime)
            .FirstOrDefaultAsync(cancellationToken);

        return new TechnicianSelfStatusDto
        {
            TechnicianId = technicianId,
            Status = technician.Status.ToString(),
            ActiveServiceTicketId = openLog?.ServiceTicketId,
            ActiveMaintenanceOrderId = openLog?.MaintenanceOrderId,
            CheckedInAt = openLog?.StartTime
        };
    }

    public async Task<TechnicianSelfStatusDto> CheckInAsync(Guid technicianId, CheckInRequest request, CancellationToken cancellationToken = default)
    {
        var technician = await _db.Technicians.FirstOrDefaultAsync(t => t.Id == technicianId, cancellationToken)
            ?? throw new NotFoundException(nameof(Technician), technicianId);

        if (technician.Status == TechnicianStatus.Ocupado)
        {
            throw new ConflictException("Ya tienes una visita en curso. Haz check-out antes de iniciar otra.");
        }

        if (request.ServiceTicketId.HasValue)
        {
            var ticket = await _db.ServiceTickets.FirstOrDefaultAsync(t => t.Id == request.ServiceTicketId, cancellationToken)
                ?? throw new NotFoundException(nameof(ServiceTicket), request.ServiceTicketId!.Value);

            if (ticket.TechnicianId != technicianId)
            {
                throw new ForbiddenException("Este ticket no está asignado a ti.");
            }

            if (ticket.Status is not (ServiceTicketStatus.Asignado or ServiceTicketStatus.EnProceso))
            {
                throw new ConflictException($"No puedes hacer check-in en un ticket en estado '{ticket.Status}'.");
            }

            ticket.Status = ServiceTicketStatus.EnProceso;
        }
        else
        {
            var order = await _db.MaintenanceOrders.FirstOrDefaultAsync(o => o.Id == request.MaintenanceOrderId, cancellationToken)
                ?? throw new NotFoundException(nameof(MaintenanceOrder), request.MaintenanceOrderId!.Value);

            if (order.TechnicianId != technicianId)
            {
                throw new ForbiddenException("Esta orden no está asignada a ti.");
            }

            if (order.Status is not (MaintenanceOrderStatus.Asignada or MaintenanceOrderStatus.EnProceso))
            {
                throw new ConflictException($"No puedes hacer check-in en una orden en estado '{order.Status}'.");
            }

            order.Status = MaintenanceOrderStatus.EnProceso;
        }

        technician.Status = TechnicianStatus.Ocupado;
        _db.TechnicianAvailabilities.Add(new TechnicianAvailability
        {
            TechnicianId = technicianId,
            Status = TechnicianStatus.Ocupado,
            Reason = "Check-in"
        });

        _db.TimeLogs.Add(new TimeLog
        {
            TechnicianId = technicianId,
            ServiceTicketId = request.ServiceTicketId,
            MaintenanceOrderId = request.MaintenanceOrderId,
            StartTime = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await GetMyStatusAsync(technicianId, cancellationToken);
    }

    public async Task<TechnicianSelfStatusDto> CheckOutAsync(Guid technicianId, CheckOutRequest request, CancellationToken cancellationToken = default)
    {
        var technician = await _db.Technicians.FirstOrDefaultAsync(t => t.Id == technicianId, cancellationToken)
            ?? throw new NotFoundException(nameof(Technician), technicianId);

        if (technician.Status != TechnicianStatus.Ocupado)
        {
            throw new ConflictException("No tienes una visita en curso para cerrar.");
        }

        var openLog = await _db.TimeLogs
            .Where(tl => tl.TechnicianId == technicianId && tl.EndTime == null)
            .OrderByDescending(tl => tl.StartTime)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ConflictException("No se encontró un registro de tiempo abierto.");

        openLog.EndTime = DateTime.UtcNow;
        openLog.Notes = request.Notes?.Trim();

        technician.Status = TechnicianStatus.Disponible;
        _db.TechnicianAvailabilities.Add(new TechnicianAvailability
        {
            TechnicianId = technicianId,
            Status = TechnicianStatus.Disponible,
            Reason = "Check-out"
        });

        await _db.SaveChangesAsync(cancellationToken);

        // El check-out solo cierra la visita; marcar Resuelto/Completada reutiliza la misma lógica
        // (y efectos secundarios, como el recálculo del cronograma) que usan los endpoints manuales de Staff.
        if (request.Resolved)
        {
            if (openLog.ServiceTicketId.HasValue)
            {
                await _ticketService.SetStatusAsync(openLog.ServiceTicketId.Value, nameof(ServiceTicketStatus.Resuelto), cancellationToken);
            }
            else if (openLog.MaintenanceOrderId.HasValue)
            {
                await _orderService.CompleteAsync(openLog.MaintenanceOrderId.Value, cancellationToken);
            }
        }

        return await GetMyStatusAsync(technicianId, cancellationToken);
    }
}
