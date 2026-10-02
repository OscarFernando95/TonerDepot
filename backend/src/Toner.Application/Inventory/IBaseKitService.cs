using Toner.Application.Inventory.Dtos;

namespace Toner.Application.Inventory;

public interface IBaseKitService
{
    Task<IReadOnlyList<BaseKitItemDto>> GetBrandKitAsync(Guid brandId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BaseKitItemDto>> SetBrandKitAsync(Guid brandId, SetBrandKitRequest request, CancellationToken cancellationToken = default);

    // Kit efectivo del modelo: lo heredado de la marca + sus ajustes.
    Task<IReadOnlyList<ModelKitItemDto>> GetModelKitAsync(Guid modelId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModelKitItemDto>> SetModelKitAsync(Guid modelId, SetModelKitRequest request, CancellationToken cancellationToken = default);
}
