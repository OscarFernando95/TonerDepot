using Microsoft.EntityFrameworkCore;
using Toner.Application.Cities.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;

namespace Toner.Application.Cities;

public class CityService : ICityService
{
    private readonly IApplicationDbContext _db;

    public CityService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CityDto> CreateAsync(CreateCityRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        var exists = await _db.Cities.AnyAsync(c => c.Name == name, cancellationToken);
        if (exists)
        {
            throw new ConflictException($"Ya existe una ciudad llamada '{name}'.");
        }

        var city = new City { Name = name, StateOrProvince = request.StateOrProvince?.Trim() };
        _db.Cities.Add(city);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(city);
    }

    public async Task<IReadOnlyList<CityDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Cities
            .OrderBy(c => c.Name)
            .Select(c => new CityDto { Id = c.Id, Name = c.Name, StateOrProvince = c.StateOrProvince })
            .ToListAsync(cancellationToken);
    }

    private static CityDto ToDto(City city) => new()
    {
        Id = city.Id,
        Name = city.Name,
        StateOrProvince = city.StateOrProvince
    };
}
