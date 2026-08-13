using Toner.Application.Contracts.Dtos;

namespace Toner.Application.Contracts;

public interface IContractAssetService
{
    Task<ContractAssetDto> AddAsync(Guid contractId, AddContractAssetRequest request, Guid changedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContractAssetDto>> ListByContractAsync(Guid contractId, CancellationToken cancellationToken = default);
    Task<ContractAssetDto> EndAsync(Guid contractId, Guid id, CancellationToken cancellationToken = default);
}
