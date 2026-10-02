using Toner.Application.Common.Paging;
using Toner.Application.Inventory.Dtos;

namespace Toner.Application.Inventory;

public interface IInventoryService
{
    Task<PagedResult<InventoryItemDto>> ListItemsAsync(string? search, string? category, bool activeOnly, int? page, int? pageSize, CancellationToken cancellationToken = default);
    Task<InventoryItemDto> CreateItemAsync(CreateInventoryItemRequest request, CancellationToken cancellationToken = default);
    Task<InventoryItemDto> UpdateItemAsync(Guid id, UpdateInventoryItemRequest request, CancellationToken cancellationToken = default);

    // Catálogo de pocas filas (la bodega principal + una por zona): no se pagina.
    Task<IReadOnlyList<InventoryLocationDto>> ListLocationsAsync(CancellationToken cancellationToken = default);
    Task<InventoryLocationDto> UpdateMainLocationAsync(UpdateMainLocationRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<StockRowDto>> ListStockAsync(Guid? locationId, Guid? itemId, string? category, bool onlyLow, int? page, int? pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<InventoryMovementDto>> ListMovementsAsync(Guid? locationId, Guid? itemId, string? cursor, int? pageSize, CancellationToken cancellationToken = default);

    Task<InventoryMovementDto> RegisterEntryAsync(RegisterEntryRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryMovementDto>> TransferAsync(TransferRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<InventoryMovementDto> AdjustAsync(AdjustStockRequest request, Guid userId, CancellationToken cancellationToken = default);
}
