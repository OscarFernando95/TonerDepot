using Toner.Application.Common;
using Toner.Application.Common.Paging;
using Toner.Application.Common.Dtos;
using Toner.Application.Tickets.Dtos;
using Toner.Domain.Entities;

namespace Toner.Application.Tickets;

public interface IServiceTicketService
{
    Task<ServiceTicketDto> CreateAsync(RequestingUser requestingUser, CreateServiceTicketRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<ServiceTicketDto>> ListAsync(RequestingUser requestingUser, int? page, int? pageSize, CancellationToken cancellationToken = default);

    // Tickets activos (no resueltos/cerrados/cancelados) de otros técnicos, en ciudades que el técnico
    // cubre. Puramente informativo salvo por los que aún no arrancó nadie (ver ClaimAsync).
    // Tickets pendientes (abiertos, sin asignar, asignados o en proceso) de las máquinas vinculadas al técnico que no
    // están a su cargo: para que se entere de lo que pasa en sus máquinas.
    Task<PagedResult<ServiceTicketDto>> ListForLinkedMachinesAsync(Guid technicianId, int? page, int? pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<ServiceTicketDto>> ListInCoverageAsync(Guid technicianId, int? page, int? pageSize, CancellationToken cancellationToken = default);
    Task<ServiceTicketDto> GetByIdAsync(RequestingUser requestingUser, Guid id, CancellationToken cancellationToken = default);
    Task<ServiceTicketDto> AssignAsync(Guid id, AssignTicketRequest request, Guid assignedByUserId, CancellationToken cancellationToken = default);

    // El propio técnico se autoasigna un ticket libre o asignado a otro técnico que aún no lo inició.
    Task<ServiceTicketDto> ClaimAsync(Guid id, Guid technicianId, CancellationToken cancellationToken = default);
    Task<ServiceTicketDto> SetStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);

    // Igual que SetStatusAsync pero SIN guardar: deja el ticket trackeado para que el caller lo
    // persista en su propio SaveChangesAsync (mismo patrón que AssetService.PrepareStatusChangeAsync).
    // Lo usa TechnicianCheckInService.CheckOutAsync, que necesita que el cierre de la visita y el
    // cambio de estado del ticket caigan en un único commit — antes eran dos, y si el segundo
    // fallaba la visita quedaba cerrada con el ticket todavía EnProceso
    // (CODE_QUALITY_AUDIT.md hallazgo #8).
    Task<ServiceTicket> PrepareStatusChangeAsync(Guid id, string status, CancellationToken cancellationToken = default);

    Task<PagedResult<AssignmentHistoryDto>> GetAssignmentHistoryAsync(Guid id, string? cursor, int? pageSize, CancellationToken cancellationToken = default);
}
