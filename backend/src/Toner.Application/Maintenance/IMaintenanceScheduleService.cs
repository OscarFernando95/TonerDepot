using Toner.Application.Maintenance.Dtos;

namespace Toner.Application.Maintenance;

public interface IMaintenanceScheduleService
{
    Task<IReadOnlyList<MaintenanceScheduleDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<MaintenanceScheduleDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MaintenanceScheduleDto> SetActiveStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    // Cronogramas de activos en ciudades cubiertas por el técnico — misma info que ListAsync, para su
    // vista de "mi zona".
    Task<IReadOnlyList<MaintenanceScheduleDto>> ListInCoverageAsync(Guid technicianId, CancellationToken cancellationToken = default);

    // Crea el cronograma de cualquier activo Instalado con contrato activo que todavía no tenga uno
    // (activos instalados antes de este rediseño). Devuelve cuántos se crearon.
    Task<int> BackfillMissingAsync(CancellationToken cancellationToken = default);
}
