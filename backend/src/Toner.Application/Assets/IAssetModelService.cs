using Toner.Application.Assets.Dtos;

namespace Toner.Application.Assets;

public interface IAssetModelService
{
    Task<IReadOnlyList<AssetModelDto>> ListAsync(Guid brandId, CancellationToken cancellationToken = default);
    Task<AssetModelDto> CreateAsync(Guid brandId, CreateAssetModelRequest request, CancellationToken cancellationToken = default);
    Task<AssetModelDto> UpdateAsync(Guid brandId, Guid id, UpdateAssetModelRequest request, CancellationToken cancellationToken = default);
}
