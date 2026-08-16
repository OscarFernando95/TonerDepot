using Toner.Application.Common;
using Toner.Application.Common.Dtos;
using Toner.Application.Maintenance.Dtos;

namespace Toner.Application.Maintenance;

public interface IMaintenanceOrderService
{
    Task<IReadOnlyList<MaintenanceOrderDto>> ListAsync(RequestingUser requestingUser, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaintenanceOrderDto>> ListByScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default);

    // Órdenes activas (no completadas/canceladas) de otros técnicos, en ciudades que el técnico cubre.
    // Puramente informativo: no da derecho a check-in, solo visibilidad de lo que pasa en su zona.
    Task<IReadOnlyList<MaintenanceOrderDto>> ListInCoverageAsync(Guid technicianId, CancellationToken cancellationToken = default);
    Task<MaintenanceOrderDto> GetByIdAsync(RequestingUser requestingUser, Guid id, CancellationToken cancellationToken = default);
    Task<MaintenanceOrderDto> AssignAsync(Guid id, AssignMaintenanceOrderRequest request, Guid assignedByUserId, CancellationToken cancellationToken = default);

    // El propio técnico se autoasigna una orden Pendiente o asignada a otro técnico que aún no la
    // inició (EnProceso queda fuera de AssignableStatuses, así que ya no se puede tomar).
    Task<MaintenanceOrderDto> ClaimAsync(Guid id, Guid technicianId, CancellationToken cancellationToken = default);
    Task<MaintenanceOrderDto> CompleteAsync(Guid id, CompleteMaintenanceOrderRequest request, Guid completedByUserId, CancellationToken cancellationToken = default);
    Task<MaintenanceOrderDto> CancelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssignmentHistoryDto>> GetAssignmentHistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
