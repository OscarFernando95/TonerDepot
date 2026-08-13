using Toner.Application.Maintenance.Dtos;

namespace Toner.Application.Maintenance;

public interface IMaintenanceScheduleService
{
    Task<MaintenanceScheduleDto> CreateAsync(CreateMaintenanceScheduleRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaintenanceScheduleDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<MaintenanceScheduleDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MaintenanceScheduleDto> UpdateAsync(Guid id, UpdateMaintenanceScheduleRequest request, CancellationToken cancellationToken = default);
    Task<MaintenanceScheduleDto> SetActiveStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}
