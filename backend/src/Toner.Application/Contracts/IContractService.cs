using Toner.Application.Common;
using Toner.Application.Common.Paging;
using Toner.Application.Contracts.Dtos;

namespace Toner.Application.Contracts;

public interface IContractService
{
    Task<ContractDto> CreateAsync(CreateContractRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<ContractDto>> ListAsync(RequestingUser requestingUser, int? page, int? pageSize, CancellationToken cancellationToken = default);
    Task<ContractDto> GetByIdAsync(RequestingUser requestingUser, Guid id, CancellationToken cancellationToken = default);
    Task<ContractDto> UpdateAsync(Guid id, UpdateContractRequest request, CancellationToken cancellationToken = default);
    Task<ContractDto> SetStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
}
