using Microsoft.EntityFrameworkCore;
using Toner.Application.Cities.Dtos;
using Toner.Application.Common.Interfaces;

namespace Toner.Application.Cities;

public class CityService : ICityService
{
    private readonly IApplicationDbContext _db;

    public CityService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CityDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Cities
            .OrderBy(c => c.Name)
            .Select(c => new CityDto { Id = c.Id, Name = c.Name, StateOrProvince = c.StateOrProvince })
            .ToListAsync(cancellationToken);
    }
}
