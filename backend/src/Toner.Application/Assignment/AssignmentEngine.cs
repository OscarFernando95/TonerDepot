using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Assignment;

public class AssignmentEngine : IAssignmentEngine
{
    private readonly IApplicationDbContext _db;

    public AssignmentEngine(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Guid?> AssignServiceTicketAsync(Guid serviceTicketId, CancellationToken cancellationToken = default)
    {
        var ticket = await _db.ServiceTickets.FirstAsync(t => t.Id == serviceTicketId, cancellationToken);

        var cityId = await _db.ClientLocations
            .Where(l => l.Id == ticket.ClientLocationId)
            .Select(l => l.CityId)
            .FirstAsync(cancellationToken);

        var candidateId = await FindCandidateAsync(cityId, cancellationToken);

        if (candidateId is null)
        {
            ticket.Status = ServiceTicketStatus.SinAsignar;
            _db.AssignmentHistories.Add(new AssignmentHistory
            {
                ServiceTicketId = serviceTicketId,
                TechnicianId = null,
                AssignedByUserId = null,
                AssignmentType = AssignmentType.Automatica,
                Reason = "No hay técnicos con cobertura disponible en la ciudad."
            });
        }
        else
        {
            ticket.TechnicianId = candidateId;
            ticket.Status = ServiceTicketStatus.Asignado;
            _db.AssignmentHistories.Add(new AssignmentHistory
            {
                ServiceTicketId = serviceTicketId,
                TechnicianId = candidateId,
                AssignedByUserId = null,
                AssignmentType = AssignmentType.Automatica,
                Reason = "Asignación automática por cobertura y carga de trabajo."
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return candidateId;
    }

    public async Task<Guid?> AssignMaintenanceOrderAsync(Guid maintenanceOrderId, CancellationToken cancellationToken = default)
    {
        var order = await _db.MaintenanceOrders.FirstAsync(o => o.Id == maintenanceOrderId, cancellationToken);

        var cityId = await _db.Assets
            .Where(a => a.Id == order.AssetId)
            .Select(a => a.CurrentClientLocation != null ? (Guid?)a.CurrentClientLocation.CityId : null)
            .FirstAsync(cancellationToken);

        var candidateId = cityId.HasValue ? await FindCandidateAsync(cityId.Value, cancellationToken) : null;

        if (candidateId is null)
        {
            _db.AssignmentHistories.Add(new AssignmentHistory
            {
                MaintenanceOrderId = maintenanceOrderId,
                TechnicianId = null,
                AssignedByUserId = null,
                AssignmentType = AssignmentType.Automatica,
                Reason = cityId.HasValue
                    ? "No hay técnicos con cobertura disponible en la ciudad."
                    : "El activo no tiene una sede de instalación asignada."
            });
        }
        else
        {
            order.TechnicianId = candidateId;
            order.Status = MaintenanceOrderStatus.Asignada;
            _db.AssignmentHistories.Add(new AssignmentHistory
            {
                MaintenanceOrderId = maintenanceOrderId,
                TechnicianId = candidateId,
                AssignedByUserId = null,
                AssignmentType = AssignmentType.Automatica,
                Reason = "Asignación automática por cobertura y carga de trabajo."
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return candidateId;
    }

    // Candidatos: técnicos activos, con cobertura en la ciudad, que no estén Ocupado ahora mismo.
    // Entre los candidatos, gana el de menor carga (tickets + órdenes actualmente asignados o en proceso).
    private async Task<Guid?> FindCandidateAsync(Guid cityId, CancellationToken cancellationToken)
    {
        var candidateIds = await _db.Technicians
            .Where(t => t.IsActive && t.Status != TechnicianStatus.Ocupado && t.Coverages.Any(c => c.CityId == cityId))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
        {
            return null;
        }

        if (candidateIds.Count == 1)
        {
            return candidateIds[0];
        }

        var ticketWorkload = await _db.ServiceTickets
            .Where(t => t.TechnicianId != null
                && candidateIds.Contains(t.TechnicianId.Value)
                && (t.Status == ServiceTicketStatus.Asignado || t.Status == ServiceTicketStatus.EnProceso))
            .GroupBy(t => t.TechnicianId!.Value)
            .Select(g => new { TechnicianId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TechnicianId, x => x.Count, cancellationToken);

        var orderWorkload = await _db.MaintenanceOrders
            .Where(o => o.TechnicianId != null
                && candidateIds.Contains(o.TechnicianId.Value)
                && (o.Status == MaintenanceOrderStatus.Asignada || o.Status == MaintenanceOrderStatus.EnProceso))
            .GroupBy(o => o.TechnicianId!.Value)
            .Select(g => new { TechnicianId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TechnicianId, x => x.Count, cancellationToken);

        return candidateIds
            .OrderBy(id => ticketWorkload.GetValueOrDefault(id) + orderWorkload.GetValueOrDefault(id))
            .First();
    }
}
