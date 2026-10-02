using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

// Libro de movimientos: el saldo de un ítem en una ubicación es la suma de sus Delta. Nunca se edita ni se borra una
// fila; una corrección es un movimiento nuevo (Ajuste), para que todo quede auditable.
public class InventoryMovement : BaseEntity
{
    public Guid InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;

    public Guid InventoryLocationId { get; set; }
    public InventoryLocation InventoryLocation { get; set; } = null!;

    public InventoryMovementType Type { get; set; }

    // Con signo: entra (+) o sale (−) de esa ubicación.
    public int Delta { get; set; }

    // Une las dos filas de un traspaso (salida de origen + entrada en destino).
    public Guid? TransferId { get; set; }

    public string? Notes { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
