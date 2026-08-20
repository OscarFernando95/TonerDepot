using Toner.Application.Contracts.Dtos;
using Toner.Application.Common.Paging;

namespace Toner.Application.Contracts;

public interface IContractAssetService
{
    Task<ContractAssetDto> AddAsync(Guid contractId, AddContractAssetRequest request, Guid changedByUserId, CancellationToken cancellationToken = default);
    Task<PagedResult<ContractAssetDto>> ListByContractAsync(Guid contractId, int? page, int? pageSize, CancellationToken cancellationToken = default);
    Task<ContractAssetDto> EndAsync(Guid contractId, Guid id, CancellationToken cancellationToken = default);
}
