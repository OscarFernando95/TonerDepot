using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Enums;

namespace Toner.Application.Assignment;

// Reintenta la asignación automática de lo que quedó sin técnico (tickets Sin asignar y órdenes de
// mantenimiento Pendientes) — típicamente porque se creó fuera de horario laboral, en festivo, o con
// todos los técnicos de la ciudad fuera de la oficina. Lo corre PendingAssignmentJob cada pocos minutos.
public class PendingAssignmentService
{
    private const int BatchSize = 200;

    private readonly IApplicationDbContext _db;
    private readonly IAssignmentEngine _engine;

    public PendingAssignmentService(IApplicationDbContext db, IAssignmentEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    // Devuelve cuántos tickets y órdenes se lograron asignar. Un único SaveChanges: o se persiste todo el
    // lote con su historial, o nada (mismo criterio que MaintenanceScheduleEvaluationJob).
    public async Task<(int Tickets, int Orders)> RunAsync(CancellationToken cancellationToken = default)
    {
        var tickets = await _db.ServiceTickets
            .Where(t => t.Status == ServiceTicketStatus.SinAsignar && t.TechnicianId == null)
            .OrderBy(t => t.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var orders = await _db.MaintenanceOrders
            .Where(o => o.Status == MaintenanceOrderStatus.Pendiente && o.TechnicianId == null)
            .OrderBy(o => o.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var assignedTickets = 0;
        foreach (var ticket in tickets)
        {
            if (await _engine.AssignServiceTicketAsync(ticket, cancellationToken, recordUnassignedAttempt: false) is not null)
            {
                assignedTickets++;
            }
        }

        var assignedOrders = 0;
        foreach (var order in orders)
        {
            if (await _engine.AssignMaintenanceOrderAsync(order, cancellationToken, recordUnassignedAttempt: false) is not null)
            {
                assignedOrders++;
            }
        }

        if (assignedTickets + assignedOrders > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return (assignedTickets, assignedOrders);
    }
}
