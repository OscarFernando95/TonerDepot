using Toner.Application.Clients.Dtos;

namespace Toner.Application.Clients;

public interface IClientService
{
    Task<ClientDto> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<ClientDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ClientDto> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken cancellationToken = default);
    Task<ClientDto> SetActiveStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}
