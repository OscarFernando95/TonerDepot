using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

// Artículo del catálogo de inventario. No lleva cantidades: el stock sale de sumar sus movimientos por ubicación.
public class InventoryItem : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public InventoryCategory Category { get; set; }
    public string? Unit { get; set; }

    // Costo por unidad, opcional: alimenta el costo por página del BI.
    public decimal? UnitCost { get; set; }

    // Debajo o igual a este saldo (en cualquier ubicación) se marca como stock bajo. 0 = sin alerta.
    public int MinimumStock { get; set; }

    // Un ítem con movimientos no se borra: se desactiva para que deje de ofrecerse.
    public bool IsActive { get; set; } = true;
}
