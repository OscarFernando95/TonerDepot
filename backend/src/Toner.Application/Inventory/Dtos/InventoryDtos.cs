namespace Toner.Application.Inventory.Dtos;

public class InventoryItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    // ConsumibleBase | Repuesto | Toner
    public string Category { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public decimal? UnitCost { get; set; }
    public int MinimumStock { get; set; }
    public bool IsActive { get; set; }
}

public class CreateInventoryItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public decimal? UnitCost { get; set; }
    public int MinimumStock { get; set; }
}

public class UpdateInventoryItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public decimal? UnitCost { get; set; }
    public int MinimumStock { get; set; }
    public bool IsActive { get; set; } = true;
}

public class InventoryLocationDto
{
    public Guid Id { get; set; }
    // Principal | Zona
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ZoneId { get; set; }
    public string? Address { get; set; }
    public Guid? CityId { get; set; }
    public string? CityName { get; set; }
}

// Sede principal de la empresa, donde está la bodega principal.
public class UpdateMainLocationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public Guid? CityId { get; set; }
}

public class StockRowDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int MinimumStock { get; set; }
    // Saldo menor o igual al mínimo del ítem (con mínimo > 0), o negativo.
    public bool IsLow { get; set; }
}

public class InventoryMovementDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Delta { get; set; }
    public string? Notes { get; set; }
    public string? CreatedByUserName { get; set; }
    public DateTime OccurredAt { get; set; }
}

public class RegisterEntryRequest
{
    public Guid LocationId { get; set; }
    public Guid ItemId { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}

public class TransferRequest
{
    public Guid ItemId { get; set; }
    public Guid FromLocationId { get; set; }
    public Guid ToLocationId { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}

// Corrección del saldo (conteo físico, merma): suma o resta, con motivo obligatorio.
public class AdjustStockRequest
{
    public Guid LocationId { get; set; }
    public Guid ItemId { get; set; }
    public int Delta { get; set; }
    public string Notes { get; set; } = string.Empty;
}

// ── Kit base ─────────────────────────────────────────────────────────────────────────────────────

public class BaseKitItemRequest
{
    public Guid ItemId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

public class SetBrandKitRequest
{
    public IReadOnlyList<BaseKitItemRequest> Items { get; set; } = Array.Empty<BaseKitItemRequest>();
}

public class BaseKitItemDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class ModelKitOverrideRequest
{
    public Guid ItemId { get; set; }
    public bool Excluded { get; set; }
    public string? GroupName { get; set; }
    public int? Quantity { get; set; }
}

// Conjunto completo de ajustes del modelo sobre el kit de su marca.
public class SetModelKitRequest
{
    public IReadOnlyList<ModelKitOverrideRequest> Overrides { get; set; } = Array.Empty<ModelKitOverrideRequest>();
}

// Kit efectivo del modelo: lo heredado de la marca más sus ajustes. Los excluidos se devuelven marcados para poder
// volver a incluirlos desde la UI.
public class ModelKitItemDto : BaseKitItemDto
{
    // Marca (heredado tal cual) | Modelo (ajustado o agregado por el modelo)
    public string Source { get; set; } = string.Empty;
    public bool Excluded { get; set; }
}

// ── Consumo en visitas y tóner ───────────────────────────────────────────────────────────────────

// Pieza usada en una visita: sale del inventario de la zona del equipo.
public class UsedPartRequest
{
    public Guid ItemId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class VisitKitItemDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    // Saldo en la ubicación de inventario de la zona del equipo.
    public int Stock { get; set; }
}

// Kit base del modelo del equipo + de dónde se descuenta.
public class VisitKitDto
{
    public Guid? AssetId { get; set; }
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    // El municipio del equipo no tiene zona (o el equipo no está catalogado): se descuenta de la bodega principal.
    public bool UsesMainWarehouse { get; set; }
    public IReadOnlyList<VisitKitItemDto> Items { get; set; } = Array.Empty<VisitKitItemDto>();
}

public class PartOptionDto
{
    public Guid ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int Stock { get; set; }
}

public class RegisterTonerRequest
{
    public Guid AssetId { get; set; }
    public Guid ItemId { get; set; }
    public int Quantity { get; set; } = 1;
    // Cuándo se entregó/cambió; por defecto, el momento del registro.
    public DateTime? OccurredAt { get; set; }
    // true: se le dejó al usuario para que lo cambie él; false: lo cambió el técnico en la máquina.
    public bool DeliveredToUser { get; set; }
    public long? CounterValue { get; set; }
    public string? Notes { get; set; }
}

public class TonerEntryDto
{
    public Guid MovementId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime OccurredAt { get; set; }
    public long? CounterValue { get; set; }
    public string? Notes { get; set; }
    public string? RegisteredBy { get; set; }
    // Si este registro dejó el stock de la zona en negativo.
    public string? StockWarning { get; set; }
}
