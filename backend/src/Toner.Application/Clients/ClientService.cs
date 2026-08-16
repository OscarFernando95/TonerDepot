using Microsoft.EntityFrameworkCore;
using Toner.Application.Clients.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;

namespace Toner.Application.Clients;

public class ClientService : IClientService
{
    private readonly IApplicationDbContext _db;

    public ClientService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ClientDto> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken = default)
    {
        // Defensivo, además del validator: el servicio puede llamarse directo (tests, otro servicio a
        // futuro) sin pasar por FluentValidation — mismo criterio que AssetService.ApplyStatusChange
        // con el área obligatoria.
        if (request.Locations is null || request.Locations.Count == 0)
        {
            throw new ConflictException("Debe indicar al menos una sede para crear el cliente.");
        }

        var client = new Client
        {
            Name = request.Name.Trim(),
            TaxId = request.TaxId?.Trim(),
            ContactName = request.ContactName?.Trim(),
            ContactEmail = request.ContactEmail?.Trim().ToLowerInvariant(),
            ContactPhone = request.ContactPhone?.Trim(),
            IsActive = true,
            IsContractClient = request.IsContractClient
        };
        _db.Clients.Add(client);

        foreach (var locationRequest in request.Locations)
        {
            _db.ClientLocations.Add(new ClientLocation
            {
                ClientId = client.Id,
                CityId = locationRequest.CityId,
                Name = locationRequest.Name.Trim(),
                Address = locationRequest.Address.Trim(),
                ContactName = locationRequest.ContactName?.Trim(),
                ContactPhone = locationRequest.ContactPhone?.Trim(),
                IsActive = true
            });
        }

        // Un solo SaveChangesAsync: Client + todas sus ClientLocation se confirman juntos, atómicamente.
        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(client.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<ClientDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await Projected(_db)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ClientDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Projected(_db)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Client), id);
    }

    public async Task<ClientDto> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Client), id);

        client.Name = request.Name.Trim();
        client.TaxId = request.TaxId?.Trim();
        client.ContactName = request.ContactName?.Trim();
        client.ContactEmail = request.ContactEmail?.Trim().ToLowerInvariant();
        client.ContactPhone = request.ContactPhone?.Trim();
        client.IsContractClient = request.IsContractClient;

        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ClientDto> SetActiveStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Client), id);

        client.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    private static IQueryable<ClientDto> Projected(IApplicationDbContext db) =>
        db.Clients.Select(c => new ClientDto
        {
            Id = c.Id,
            Name = c.Name,
            TaxId = c.TaxId,
            ContactName = c.ContactName,
            ContactEmail = c.ContactEmail,
            ContactPhone = c.ContactPhone,
            IsActive = c.IsActive,
            IsContractClient = c.IsContractClient,
            LocationCount = c.Locations.Count,
            CityNames = c.Locations.Select(l => l.City.Name).Distinct().OrderBy(n => n).ToList(),
            CreatedAt = c.CreatedAt
        });
}
