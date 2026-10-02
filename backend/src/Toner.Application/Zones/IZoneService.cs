using Toner.Application.Zones.Dtos;

namespace Toner.Application.Zones;

public interface IZoneService
{
    Task<IReadOnlyList<ZoneDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<ZoneDto> CreateAsync(CreateZoneRequest request, CancellationToken cancellationToken = default);
    Task<ZoneDto> RenameAsync(Guid id, UpdateZoneRequest request, CancellationToken cancellationToken = default);
    Task<ZoneDto> SetCitiesAsync(Guid id, SetZoneCitiesRequest request, CancellationToken cancellationToken = default);

    // No se puede borrar una zona con técnicos asignados; sus municipios quedan sin zona.
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
