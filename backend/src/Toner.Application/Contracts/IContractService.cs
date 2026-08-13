using Toner.Application.Common;
using Toner.Application.Contracts.Dtos;

namespace Toner.Application.Contracts;

public interface IContractService
{
    Task<ContractDto> CreateAsync(CreateContractRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContractDto>> ListAsync(RequestingUser requestingUser, CancellationToken cancellationToken = default);
    Task<ContractDto> GetByIdAsync(RequestingUser requestingUser, Guid id, CancellationToken cancellationToken = default);
    Task<ContractDto> UpdateAsync(Guid id, UpdateContractRequest request, CancellationToken cancellationToken = default);
    Task<ContractDto> SetStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
}
