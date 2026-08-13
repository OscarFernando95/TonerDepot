using Toner.Application.Assets.Dtos;
using Toner.Application.Common;

namespace Toner.Application.Assets;

public interface IAssetService
{
    Task<AssetDto> CreateAsync(CreateAssetRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetDto>> ListAsync(RequestingUser requestingUser, CancellationToken cancellationToken = default);
    Task<AssetDto> GetByIdAsync(RequestingUser requestingUser, Guid id, CancellationToken cancellationToken = default);
    Task<AssetDto> UpdateAsync(Guid id, UpdateAssetRequest request, CancellationToken cancellationToken = default);
    Task<AssetDto> ChangeStatusAsync(Guid id, ChangeAssetStatusRequest request, Guid changedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetStatusLogDto>> GetStatusHistoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MeterReadingDto> AddMeterReadingAsync(
        Guid id,
        CreateMeterReadingRequest request,
        Guid registeredByUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MeterReadingDto>> GetMeterReadingsAsync(Guid id, CancellationToken cancellationToken = default);
}
