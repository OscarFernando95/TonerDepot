using Toner.Application.Technicians.Dtos;
using Toner.Application.Common.Paging;

namespace Toner.Application.Technicians;

public interface ITechnicianService
{
    Task<PagedResult<TechnicianDto>> ListAsync(int? page, int? pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TechnicianZoneDto>> ListZonesAsync(Guid technicianId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TechnicianZoneDto>> SetZonesAsync(Guid technicianId, SetTechnicianZonesRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TechnicianAssetDto>> ListLinkedAssetsAsync(Guid technicianId, CancellationToken cancellationToken = default);
    Task<TechnicianAssetDto> LinkAssetAsync(Guid technicianId, AddTechnicianAssetRequest request, CancellationToken cancellationToken = default);
    Task UnlinkAssetAsync(Guid technicianId, Guid technicianAssetId, CancellationToken cancellationToken = default);
    Task<PagedResult<TimeLogDto>> ListTimeLogsAsync(Guid technicianId, string? cursor, int? pageSize, CancellationToken cancellationToken = default);
}
