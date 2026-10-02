using Toner.Application.Common;
using Toner.Application.Common.Paging;
using Toner.Application.Inventory.Dtos;

namespace Toner.Application.Inventory;

// A qué visita pertenece un consumo: de ahí salen el cliente, la máquina y el contador del movimiento.
public sealed record VisitConsumption(
    Guid? ClientId, Guid? AssetId, Guid? MaintenanceOrderId, Guid? ServiceTicketId, Guid? TimeLogId, long? CounterValue, Guid UserId);

public interface IInventoryConsumptionService
{
    // Agrega (SIN guardar) los movimientos de consumo de las piezas usadas en una visita, descontando del inventario de
    // la zona del equipo. El caller los persiste en su propio SaveChanges (misma operación de negocio). Devuelve los
    // avisos de stock negativo: nunca bloquea el cierre de la visita.
    Task<IReadOnlyList<string>> PrepareVisitConsumptionAsync(
        VisitConsumption visit, IReadOnlyList<UsedPartRequest> parts, CancellationToken cancellationToken = default);

    // Kit base (efectivo) del modelo del equipo, con el saldo de la ubicación de donde se descontaría.
    Task<VisitKitDto> GetKitForAssetAsync(Guid? assetId, CancellationToken cancellationToken = default);

    // Búsqueda de repuestos con el saldo de esa misma ubicación.
    Task<PagedResult<PartOptionDto>> SearchPartsAsync(Guid? assetId, string? search, int? page, int? pageSize, CancellationToken cancellationToken = default, string? category = null);

    // Tóner entregado o cambiado en una máquina. Un técnico solo puede en las máquinas que tiene vinculadas.
    Task<TonerEntryDto> RegisterTonerAsync(RegisterTonerRequest request, RequestingUser requester, CancellationToken cancellationToken = default);
    Task<PagedResult<TonerEntryDto>> ListTonerAsync(
        Guid assetId, DateTime? from, DateTime? to, string? cursor, int? pageSize, RequestingUser requester, CancellationToken cancellationToken = default);
}
