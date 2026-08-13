using Toner.Application.Clients.Dtos;

namespace Toner.Application.Clients;

public interface IClientLocationService
{
    Task<ClientLocationDto> CreateAsync(Guid clientId, CreateClientLocationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientLocationDto>> ListByClientAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientLocationDto>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<ClientLocationDto> UpdateAsync(Guid clientId, Guid id, UpdateClientLocationRequest request, CancellationToken cancellationToken = default);
    Task<ClientLocationDto> SetActiveStatusAsync(Guid clientId, Guid id, bool isActive, CancellationToken cancellationToken = default);
}
