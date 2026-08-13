using Toner.Application.Cities.Dtos;

namespace Toner.Application.Cities;

public interface ICityService
{
    Task<CityDto> CreateAsync(CreateCityRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CityDto>> ListAsync(CancellationToken cancellationToken = default);
}
