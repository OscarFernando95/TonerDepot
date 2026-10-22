using Toner.Application.Clients.Dtos;
using Toner.Application.Common;
using Toner.Application.Common.Paging;

namespace Toner.Application.Clients;

public interface IClientLocationService
{
    Task<ClientLocationDto> CreateAsync(Guid clientId, CreateClientLocationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientLocationDto>> ListByClientAsync(Guid clientId, CancellationToken cancellationToken = default);

    // Las sedes de su empresa que un usuario Cliente puede ver (todas, o solo las que le asignaron).
    Task<IReadOnlyList<ClientLocationDto>> ListForClientUserAsync(RequestingUser user, CancellationToken cancellationToken = default);
    Task<PagedResult<ClientLocationDto>> ListAllAsync(int? page, int? pageSize, CancellationToken cancellationToken = default);
    Task<ClientLocationDto> UpdateAsync(Guid clientId, Guid id, UpdateClientLocationRequest request, CancellationToken cancellationToken = default);
    Task<ClientLocationDto> SetActiveStatusAsync(Guid clientId, Guid id, bool isActive, CancellationToken cancellationToken = default);
}
