using Toner.Application.Common;
using Toner.Application.Common.Dtos;
using Toner.Application.Tickets.Dtos;

namespace Toner.Application.Tickets;

public interface IServiceTicketService
{
    Task<ServiceTicketDto> CreateAsync(RequestingUser requestingUser, CreateServiceTicketRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceTicketDto>> ListAsync(RequestingUser requestingUser, CancellationToken cancellationToken = default);
    Task<ServiceTicketDto> GetByIdAsync(RequestingUser requestingUser, Guid id, CancellationToken cancellationToken = default);
    Task<ServiceTicketDto> AssignAsync(Guid id, AssignTicketRequest request, Guid assignedByUserId, CancellationToken cancellationToken = default);
    Task<ServiceTicketDto> SetStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssignmentHistoryDto>> GetAssignmentHistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
