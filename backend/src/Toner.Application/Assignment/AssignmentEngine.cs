using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Assignment;

public class AssignmentEngine : IAssignmentEngine
{
    private readonly IApplicationDbContext _db;

    // Carga asignada durante la vida de esta instancia y todavía SIN guardar. Al dejar de hacer
    // SaveChangesAsync por asignación (hallazgo #8), las consultas de carga de trabajo ya no ven lo
    // asignado hace un instante: un lote de 40 órdenes le vería a todos los técnicos la misma carga
    // inicial y se las apilaría todas al mismo. Este acumulador conserva el reparto por carga que
    // antes daba, de rebote, el commit intermedio. El servicio es scoped, así que su vida es la de
    // la request o la del job — exactamente el alcance del lote.
    private readonly Dictionary<Guid, int> _pendingWorkload = new();

    public AssignmentEngine(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Guid?> AssignServiceTicketAsync(ServiceTicket ticket, CancellationToken cancellationToken = default)
    {
        // La sede sí se lee de la base: existe desde antes que el ticket, no es parte de lo que está
        // por guardarse.
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
                ServiceTicketId = ticket.Id,
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
                ServiceTicketId = ticket.Id,
                TechnicianId = candidateId,
                AssignedByUserId = null,
                AssignmentType = AssignmentType.Automatica,
                Reason = "Asignación automática por cobertura y carga de trabajo."
            });

            _pendingWorkload[candidateId.Value] = _pendingWorkload.GetValueOrDefault(candidateId.Value) + 1;
        }

        return candidateId;
    }

    public async Task<Guid?> AssignMaintenanceOrderAsync(MaintenanceOrder order, CancellationToken cancellationToken = default)
    {
        var cityId = await _db.Assets
            .Where(a => a.Id == order.AssetId)
            .Select(a => a.CurrentClientLocation != null ? (Guid?)a.CurrentClientLocation.CityId : null)
            .FirstAsync(cancellationToken);

        var candidateId = cityId.HasValue ? await FindCandidateAsync(cityId.Value, cancellationToken) : null;

        if (candidateId is null)
        {
            _db.AssignmentHistories.Add(new AssignmentHistory
            {
                MaintenanceOrderId = order.Id,
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
                MaintenanceOrderId = order.Id,
                TechnicianId = candidateId,
                AssignedByUserId = null,
                AssignmentType = AssignmentType.Automatica,
                Reason = "Asignación automática por cobertura y carga de trabajo."
            });

            _pendingWorkload[candidateId.Value] = _pendingWorkload.GetValueOrDefault(candidateId.Value) + 1;
        }

        return candidateId;
    }

    // Candidatos: técnicos activos, con cobertura en la ciudad, que no estén Ocupado ahora mismo.
    // Entre los candidatos, gana el de menor carga (tickets + órdenes actualmente asignados o en
    // proceso, más lo ya asignado en este mismo lote y aún sin guardar).
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
            .OrderBy(id => ticketWorkload.GetValueOrDefault(id)
                + orderWorkload.GetValueOrDefault(id)
                + _pendingWorkload.GetValueOrDefault(id))
            .First();
    }
}
