using Toner.Application.Common;
using Toner.Application.Common.Dtos;
using Toner.Application.Maintenance.Dtos;

namespace Toner.Application.Maintenance;

public interface IMaintenanceOrderService
{
    Task<IReadOnlyList<MaintenanceOrderDto>> ListAsync(RequestingUser requestingUser, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaintenanceOrderDto>> ListByScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default);
    Task<MaintenanceOrderDto> GetByIdAsync(RequestingUser requestingUser, Guid id, CancellationToken cancellationToken = default);
    Task<MaintenanceOrderDto> AssignAsync(Guid id, AssignMaintenanceOrderRequest request, Guid assignedByUserId, CancellationToken cancellationToken = default);
    Task<MaintenanceOrderDto> CompleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MaintenanceOrderDto> CancelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssignmentHistoryDto>> GetAssignmentHistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
