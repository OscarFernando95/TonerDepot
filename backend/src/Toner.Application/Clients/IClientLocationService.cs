using Toner.Application.Clients.Dtos;
using Toner.Application.Common.Paging;

namespace Toner.Application.Clients;

public interface IClientLocationService
{
    Task<ClientLocationDto> CreateAsync(Guid clientId, CreateClientLocationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientLocationDto>> ListByClientAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<PagedResult<ClientLocationDto>> ListAllAsync(int? page, int? pageSize, CancellationToken cancellationToken = default);
    Task<ClientLocationDto> UpdateAsync(Guid clientId, Guid id, UpdateClientLocationRequest request, CancellationToken cancellationToken = default);
    Task<ClientLocationDto> SetActiveStatusAsync(Guid clientId, Guid id, bool isActive, CancellationToken cancellationToken = default);
}
