using Toner.Application.Cities.Dtos;

namespace Toner.Application.Cities;

// Solo lectura: las ciudades son un catálogo fijo (sembrado desde el dataset de municipios de
// Colombia, ver DataSeeder), ya no se crean a mano.
public interface ICityService
{
    Task<IReadOnlyList<CityDto>> ListAsync(CancellationToken cancellationToken = default);
}
