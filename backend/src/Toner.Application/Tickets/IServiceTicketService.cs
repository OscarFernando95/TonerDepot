using Toner.Application.Common;
using Toner.Application.Common.Dtos;
using Toner.Application.Tickets.Dtos;

namespace Toner.Application.Tickets;

public interface IServiceTicketService
{
    Task<ServiceTicketDto> CreateAsync(RequestingUser requestingUser, CreateServiceTicketRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceTicketDto>> ListAsync(RequestingUser requestingUser, CancellationToken cancellationToken = default);

    // Tickets activos (no resueltos/cerrados/cancelados) de otros técnicos, en ciudades que el técnico
    // cubre. Puramente informativo salvo por los que aún no arrancó nadie (ver ClaimAsync).
    Task<IReadOnlyList<ServiceTicketDto>> ListInCoverageAsync(Guid technicianId, CancellationToken cancellationToken = default);
    Task<ServiceTicketDto> GetByIdAsync(RequestingUser requestingUser, Guid id, CancellationToken cancellationToken = default);
    Task<ServiceTicketDto> AssignAsync(Guid id, AssignTicketRequest request, Guid assignedByUserId, CancellationToken cancellationToken = default);

    // El propio técnico se autoasigna un ticket libre o asignado a otro técnico que aún no lo inició.
    Task<ServiceTicketDto> ClaimAsync(Guid id, Guid technicianId, CancellationToken cancellationToken = default);
    Task<ServiceTicketDto> SetStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssignmentHistoryDto>> GetAssignmentHistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
