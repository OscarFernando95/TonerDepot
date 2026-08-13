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
        var client = new Client
        {
            Name = request.Name.Trim(),
            TaxId = request.TaxId?.Trim(),
            ContactName = request.ContactName?.Trim(),
            ContactEmail = request.ContactEmail?.Trim().ToLowerInvariant(),
            ContactPhone = request.ContactPhone?.Trim(),
            IsActive = true
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(client, locationCount: 0);
    }

    public async Task<IReadOnlyList<ClientDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Clients
            .OrderBy(c => c.Name)
            .Select(c => new ClientDto
            {
                Id = c.Id,
                Name = c.Name,
                TaxId = c.TaxId,
                ContactName = c.ContactName,
                ContactEmail = c.ContactEmail,
                ContactPhone = c.ContactPhone,
                IsActive = c.IsActive,
                LocationCount = c.Locations.Count,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ClientDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Clients
            .Where(c => c.Id == id)
            .Select(c => new ClientDto
            {
                Id = c.Id,
                Name = c.Name,
                TaxId = c.TaxId,
                ContactName = c.ContactName,
                ContactEmail = c.ContactEmail,
                ContactPhone = c.ContactPhone,
                IsActive = c.IsActive,
                LocationCount = c.Locations.Count,
                CreatedAt = c.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken)
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

    private static ClientDto ToDto(Client client, int locationCount) => new()
    {
        Id = client.Id,
        Name = client.Name,
        TaxId = client.TaxId,
        ContactName = client.ContactName,
        ContactEmail = client.ContactEmail,
        ContactPhone = client.ContactPhone,
        IsActive = client.IsActive,
        LocationCount = locationCount,
        CreatedAt = client.CreatedAt
    };
}
