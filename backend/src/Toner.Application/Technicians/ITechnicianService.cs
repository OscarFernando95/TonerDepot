using Toner.Application.Technicians.Dtos;

namespace Toner.Application.Technicians;

public interface ITechnicianService
{
    Task<IReadOnlyList<TechnicianDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TechnicianCoverageDto>> ListCoverageAsync(Guid technicianId, CancellationToken cancellationToken = default);
    Task<TechnicianCoverageDto> AddCoverageAsync(Guid technicianId, AddTechnicianCoverageRequest request, CancellationToken cancellationToken = default);
    Task RemoveCoverageAsync(Guid technicianId, Guid coverageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeLogDto>> ListTimeLogsAsync(Guid technicianId, CancellationToken cancellationToken = default);
}
