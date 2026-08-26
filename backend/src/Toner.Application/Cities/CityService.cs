using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Toner.Application.Cities.Dtos;
using Toner.Application.Common.Caching;
using Toner.Application.Common.Interfaces;

namespace Toner.Application.Cities;

public class CityService : ICityService
{
    private readonly IApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public CityService(IApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    // El mejor candidato a caché del proyecto (CODE_QUALITY_AUDIT.md hallazgo #10): ~1.100 filas
    // sembradas por migración, SIN ningún endpoint de escritura, y consultadas cada vez que se abre
    // un formulario con desplegable de ciudad (CreateClientDialog, UsersView, ClientDetailView).
    //
    // Sin RLS: Cities no está entre las tablas con políticas (Assets, ClientLocations, Contracts,
    // ServiceTickets), así que el resultado es el mismo para cualquier usuario y una sola entrada
    // compartida es correcta.
    public async Task<IReadOnlyList<CityDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKeys.Cities, out IReadOnlyList<CityDto>? cached) && cached is not null)
        {
            return cached;
        }

        var cities = await _db.Cities
            .OrderBy(c => c.Name)
            .Select(c => new CityDto { Id = c.Id, Name = c.Name, StateOrProvince = c.StateOrProvince })
            .ToListAsync(cancellationToken);

        // Absoluta, no deslizante: con expiración deslizante un endpoint con tráfico constante nunca
        // se refrescaría. Acá además no hay invalidación activa posible (no existe escritura de
        // ciudades), así que el techo de antigüedad tiene que ser real.
        _cache.Set(CacheKeys.Cities, (IReadOnlyList<CityDto>)cities, CacheDurations.Catalog);

        return cities;
    }
}
