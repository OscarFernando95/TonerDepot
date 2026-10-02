using Toner.Application.Common;
using Toner.Application.Common.Paging;
using Toner.Application.Common.Dtos;
using Toner.Application.Maintenance.Dtos;
using Toner.Domain.Entities;

namespace Toner.Application.Maintenance;

public interface IMaintenanceOrderService
{
    Task<PagedResult<MaintenanceOrderDto>> ListAsync(RequestingUser requestingUser, int? page, int? pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<MaintenanceOrderDto>> ListByScheduleAsync(Guid scheduleId, string? cursor, int? pageSize, CancellationToken cancellationToken = default);

    // Órdenes activas (no completadas/canceladas) de otros técnicos, en ciudades que el técnico cubre.
    // Puramente informativo: no da derecho a check-in, solo visibilidad de lo que pasa en su zona.
    Task<PagedResult<MaintenanceOrderDto>> ListInCoverageAsync(Guid technicianId, int? page, int? pageSize, CancellationToken cancellationToken = default);
    Task<MaintenanceOrderDto> GetByIdAsync(RequestingUser requestingUser, Guid id, CancellationToken cancellationToken = default);
    // Mantenimiento a demanda de un equipo instalado: crea la orden y la asigna por el mismo motor que las
    // programadas (si no hay técnico disponible queda Pendiente y el job la recoge).
    Task<MaintenanceOrderDto> CreateManualAsync(CreateManualMaintenanceOrderRequest request, Guid requestedByUserId, CancellationToken cancellationToken = default);
    Task<MaintenanceOrderDto> AssignAsync(Guid id, AssignMaintenanceOrderRequest request, Guid assignedByUserId, CancellationToken cancellationToken = default);

    // El propio técnico se autoasigna una orden Pendiente o asignada a otro técnico que aún no la
    // inició (EnProceso queda fuera de AssignableStatuses, así que ya no se puede tomar).
    Task<MaintenanceOrderDto> ClaimAsync(Guid id, Guid technicianId, CancellationToken cancellationToken = default);
    Task<MaintenanceOrderDto> CompleteAsync(Guid id, CompleteMaintenanceOrderRequest request, Guid completedByUserId, CancellationToken cancellationToken = default);

    // Igual que CompleteAsync pero SIN guardar: deja la orden, la MeterReading y el recálculo del
    // cronograma trackeados para que el caller los persista en su propio SaveChangesAsync (mismo
    // patrón que AssetService.PrepareStatusChangeAsync). Lo usa
    // TechnicianCheckInService.CheckOutAsync — antes esto era un commit aparte, y si fallaba la
    // visita quedaba cerrada con la orden todavía EnProceso y el cronograma sin avanzar
    // (CODE_QUALITY_AUDIT.md hallazgo #8).
    Task<MaintenanceOrder> PrepareCompleteAsync(Guid id, CompleteMaintenanceOrderRequest request, Guid completedByUserId, CancellationToken cancellationToken = default);
    Task<MaintenanceOrderDto> CancelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AssignmentHistoryDto>> GetAssignmentHistoryAsync(Guid id, string? cursor, int? pageSize, CancellationToken cancellationToken = default);
}
