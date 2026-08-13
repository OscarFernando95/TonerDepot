using Microsoft.EntityFrameworkCore;
using Toner.Application.Clients.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;

namespace Toner.Application.Clients;

public class ClientLocationService : IClientLocationService
{
    private readonly IApplicationDbContext _db;

    public ClientLocationService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ClientLocationDto> CreateAsync(Guid clientId, CreateClientLocationRequest request, CancellationToken cancellationToken = default)
    {
        var clientExists = await _db.Clients.AnyAsync(c => c.Id == clientId, cancellationToken);
        if (!clientExists)
        {
            throw new NotFoundException(nameof(Client), clientId);
        }

        var location = new ClientLocation
        {
            ClientId = clientId,
            CityId = request.CityId,
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            ContactName = request.ContactName?.Trim(),
            ContactPhone = request.ContactPhone?.Trim(),
            IsActive = true
        };

        _db.ClientLocations.Add(location);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(location.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<ClientLocationDto>> ListByClientAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        return await Projected(_db).Where(l => l.ClientId == clientId).OrderBy(l => l.Name).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClientLocationDto>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await Projected(_db)
            .OrderBy(l => l.ClientName)
            .ThenBy(l => l.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ClientLocationDto> UpdateAsync(Guid clientId, Guid id, UpdateClientLocationRequest request, CancellationToken cancellationToken = default)
    {
        var location = await _db.ClientLocations.FirstOrDefaultAsync(l => l.Id == id && l.ClientId == clientId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClientLocation), id);

        location.CityId = request.CityId;
        location.Name = request.Name.Trim();
        location.Address = request.Address.Trim();
        location.ContactName = request.ContactName?.Trim();
        location.ContactPhone = request.ContactPhone?.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    public async Task<ClientLocationDto> SetActiveStatusAsync(Guid clientId, Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var location = await _db.ClientLocations.FirstOrDefaultAsync(l => l.Id == id && l.ClientId == clientId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClientLocation), id);

        location.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    private async Task<ClientLocationDto> ToDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        return await Projected(_db).FirstAsync(l => l.Id == id, cancellationToken);
    }

    private static IQueryable<ClientLocationDto> Projected(IApplicationDbContext db) =>
        db.ClientLocations.Select(l => new ClientLocationDto
        {
            Id = l.Id,
            ClientId = l.ClientId,
            ClientName = l.Client.Name,
            CityId = l.CityId,
            CityName = l.City.Name,
            Name = l.Name,
            Address = l.Address,
            ContactName = l.ContactName,
            ContactPhone = l.ContactPhone,
            IsActive = l.IsActive,
            CreatedAt = l.CreatedAt
        });
}
