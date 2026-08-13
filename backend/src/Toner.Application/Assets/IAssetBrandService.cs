using Toner.Application.Assets.Dtos;

namespace Toner.Application.Assets;

public interface IAssetBrandService
{
    Task<AssetBrandDto> CreateAsync(CreateAssetBrandRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetBrandDto>> ListAsync(CancellationToken cancellationToken = default);
}
