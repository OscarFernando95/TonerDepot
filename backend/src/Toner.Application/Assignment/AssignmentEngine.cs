using Microsoft.EntityFrameworkCore;
using Toner.Application.Calendar;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Assignment;

public class AssignmentEngine : IAssignmentEngine
{
    private readonly IApplicationDbContext _db;
    private readonly IWorkCalendarService _calendar;
    private readonly TimeProvider _time;

    // Carga asignada durante la vida de esta instancia y todavía SIN guardar. Al dejar de hacer
    // SaveChangesAsync por asignación (hallazgo #8), las consultas de carga de trabajo ya no ven lo
    // asignado hace un instante: un lote de 40 órdenes le vería a todos los técnicos la misma carga
    // inicial y se las apilaría todas al mismo. Este acumulador conserva el reparto por carga que
    // antes daba, de rebote, el commit intermedio. El servicio es scoped, así que su vida es la de
    // la request o la del job — exactamente el alcance del lote.
    private readonly Dictionary<Guid, int> _pendingWorkload = new();

    public AssignmentEngine(IApplicationDbContext db, IWorkCalendarService calendar, TimeProvider time)
    {
        _db = db;
        _calendar = calendar;
        _time = time;
    }

    public async Task<Guid?> AssignServiceTicketAsync(ServiceTicket ticket, CancellationToken cancellationToken = default, bool recordUnassignedAttempt = true)
    {
        // La sede sí se lee de la base: existe desde antes que el ticket, no es parte de lo que está
        // por guardarse.
        var cityId = await _db.ClientLocations
            .Where(l => l.Id == ticket.ClientLocationId)
            .Select(l => l.CityId)
            .FirstAsync(cancellationToken);

        var candidate = await FindCandidateAsync(cityId, await GetCoverageAsync(ticket.ClientId, cancellationToken), cancellationToken);
        var candidateId = candidate.TechnicianId;

        if (candidateId is null)
        {
            ticket.Status = ServiceTicketStatus.SinAsignar;
            if (recordUnassignedAttempt)
            {
                _db.AssignmentHistories.Add(new AssignmentHistory
                {
                    ServiceTicketId = ticket.Id,
                    ClientId = ticket.ClientId,
                    TechnicianId = null,
                    AssignedByUserId = null,
                    AssignmentType = AssignmentType.Automatica,
                    Reason = candidate.OutOfScheduleOnly
                        ? "Hay técnicos con cobertura en la ciudad, pero están fuera de su horario laboral o fuera de la oficina."
                        : "No hay técnicos con cobertura disponible en la ciudad."
                });
            }
        }
        else
        {
            ticket.TechnicianId = candidateId;
            ticket.Status = ServiceTicketStatus.Asignado;
            _db.AssignmentHistories.Add(new AssignmentHistory
            {
                ServiceTicketId = ticket.Id,
                ClientId = ticket.ClientId,
                TechnicianId = candidateId,
                AssignedByUserId = null,
                AssignmentType = AssignmentType.Automatica,
                Reason = "Asignación automática por cobertura y carga de trabajo."
            });

            _pendingWorkload[candidateId.Value] = _pendingWorkload.GetValueOrDefault(candidateId.Value) + 1;
        }

        return candidateId;
    }

    public async Task<Guid?> AssignMaintenanceOrderAsync(MaintenanceOrder order, CancellationToken cancellationToken = default, bool recordUnassignedAttempt = true)
    {
        var cityId = await _db.Assets
            .Where(a => a.Id == order.AssetId)
            .Select(a => a.CurrentClientLocation != null ? (Guid?)a.CurrentClientLocation.CityId : null)
            .FirstAsync(cancellationToken);

        var candidate = cityId.HasValue
            ? await FindCandidateAsync(cityId.Value, await GetCoverageAsync(order.ClientId, cancellationToken), cancellationToken)
            : new Candidate(null, false);
        var candidateId = candidate.TechnicianId;

        if (candidateId is null)
        {
            if (recordUnassignedAttempt)
            {
                _db.AssignmentHistories.Add(new AssignmentHistory
                {
                    MaintenanceOrderId = order.Id,
                    ClientId = order.ClientId,
                    TechnicianId = null,
                    AssignedByUserId = null,
                    AssignmentType = AssignmentType.Automatica,
                    Reason = !cityId.HasValue
                        ? "El activo no tiene una sede de instalación asignada."
                        : candidate.OutOfScheduleOnly
                            ? "Hay técnicos con cobertura en la ciudad, pero están fuera de su horario laboral o fuera de la oficina."
                            : "No hay técnicos con cobertura disponible en la ciudad."
                });
            }
        }
        else
        {
            order.TechnicianId = candidateId;
            order.Status = MaintenanceOrderStatus.Asignada;
            _db.AssignmentHistories.Add(new AssignmentHistory
            {
                MaintenanceOrderId = order.Id,
                ClientId = order.ClientId,
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
    private readonly record struct Candidate(Guid? TechnicianId, bool OutOfScheduleOnly);

    private async Task<SupportCoverage> GetCoverageAsync(Guid clientId, CancellationToken cancellationToken) =>
        await _db.Clients.Where(c => c.Id == clientId).Select(c => c.SupportCoverage).FirstOrDefaultAsync(cancellationToken);

    private async Task<Candidate> FindCandidateAsync(Guid cityId, SupportCoverage coverage, CancellationToken cancellationToken)
    {
        var coveringIds = await _db.Technicians
            .Where(t => t.IsActive && t.Status != TechnicianStatus.Ocupado && t.Coverages.Any(c => c.CityId == cityId))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (coveringIds.Count == 0)
        {
            return new Candidate(null, false);
        }

        // Fuera de horario laboral / de vacaciones no se asigna (salvo clientes 24/7, que ignoran el
        // horario pero no los permisos). Así las tareas fuera de horario quedan Sin asignar hasta que
        // PendingAssignmentJob las recoja al entrar alguien en horario.
        var now = _time.GetUtcNow().UtcDateTime;
        var calendar = await _calendar.LoadAsync(coveringIds, now, now.AddMinutes(1), cancellationToken);
        var candidateIds = coveringIds.Where(id => calendar.IsAssignable(id, coverage, now)).ToList();

        if (candidateIds.Count == 0)
        {
            return new Candidate(null, true);
        }

        if (candidateIds.Count == 1)
        {
            return new Candidate(candidateIds[0], false);
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

        var winner = candidateIds
            .OrderBy(id => ticketWorkload.GetValueOrDefault(id)
                + orderWorkload.GetValueOrDefault(id)
                + _pendingWorkload.GetValueOrDefault(id))
            .First();

        return new Candidate(winner, false);
    }
}
