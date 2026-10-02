using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Zones.Dtos;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Zones;

public class ZoneService : IZoneService
{
    private readonly IApplicationDbContext _db;

    public ZoneService(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ZoneDto>> ListAsync(CancellationToken cancellationToken = default) =>
        await Projected(_db.Zones).OrderBy(z => z.Name).ToListAsync(cancellationToken);

    public async Task<ZoneDto> CreateAsync(CreateZoneRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        await EnsureNameAvailableAsync(name, null, cancellationToken);

        var zone = new Zone { Name = name };
        _db.Zones.Add(zone);
        // Cada zona lleva su propio inventario: la ubicación nace con ella (mismo SaveChanges).
        _db.InventoryLocations.Add(new InventoryLocation { Kind = InventoryLocationKind.Zona, Zone = zone });
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(zone.Id, cancellationToken);
    }

    public async Task<ZoneDto> RenameAsync(Guid id, UpdateZoneRequest request, CancellationToken cancellationToken = default)
    {
        var zone = await _db.Zones.FirstOrDefaultAsync(z => z.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Zone), id);

        var name = request.Name.Trim();
        await EnsureNameAvailableAsync(name, id, cancellationToken);

        zone.Name = name;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<ZoneDto> SetCitiesAsync(Guid id, SetZoneCitiesRequest request, CancellationToken cancellationToken = default)
    {
        var zoneExists = await _db.Zones.AnyAsync(z => z.Id == id, cancellationToken);
        if (!zoneExists)
        {
            throw new NotFoundException(nameof(Zone), id);
        }

        var wanted = request.CityIds.Distinct().ToList();

        // Los que ya estaban y ya no se quieren quedan sin zona; los pedidos se mueven aquí aunque estén en otra.
        var affected = await _db.Cities
            .Where(c => c.ZoneId == id || wanted.Contains(c.Id))
            .ToListAsync(cancellationToken);

        foreach (var city in affected)
        {
            city.ZoneId = wanted.Contains(city.Id) ? id : null;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var zone = await _db.Zones.FirstOrDefaultAsync(z => z.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Zone), id);

        var hasTechnicians = await _db.TechnicianZones.AnyAsync(tz => tz.ZoneId == id, cancellationToken);
        if (hasTechnicians)
        {
            throw new ConflictException("No se puede eliminar una zona con técnicos asignados. Reasígnalos primero.");
        }

        // Una zona con historial de inventario no se borra: se perdería la trazabilidad de sus movimientos.
        var location = await _db.InventoryLocations.FirstOrDefaultAsync(l => l.ZoneId == id, cancellationToken);
        if (location is not null)
        {
            if (await _db.InventoryMovements.AnyAsync(m => m.InventoryLocationId == location.Id, cancellationToken))
            {
                throw new ConflictException("La zona tiene movimientos de inventario y no se puede eliminar.");
            }

            _db.InventoryLocations.Remove(location);
        }

        // Sus municipios quedan sin zona. Se hace explícito (además del SetNull de la FK) para no depender del proveedor.
        var cities = await _db.Cities.Where(c => c.ZoneId == id).ToListAsync(cancellationToken);
        foreach (var city in cities)
        {
            city.ZoneId = null;
        }

        _db.Zones.Remove(zone);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureNameAvailableAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var lower = name.ToLower();
        var taken = await _db.Zones.AnyAsync(z => z.Name.ToLower() == lower && z.Id != exceptId, cancellationToken);
        if (taken)
        {
            throw new ConflictException($"Ya existe una zona llamada '{name}'.");
        }
    }

    private async Task<ZoneDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await Projected(_db.Zones.Where(z => z.Id == id)).FirstAsync(cancellationToken);

    private static IQueryable<ZoneDto> Projected(IQueryable<Zone> query) =>
        query.Select(z => new ZoneDto
        {
            Id = z.Id,
            Name = z.Name,
            TechnicianCount = z.TechnicianZones.Count,
            Cities = z.Cities
                .OrderBy(c => c.Name)
                .Select(c => new ZoneCityDto { Id = c.Id, Name = c.Name, StateOrProvince = c.StateOrProvince })
                .ToList()
        });
}
