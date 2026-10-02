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

    // Solo el consumo (Type = Consumo) está atado a un cliente y a una máquina: ClientId lleva la política RLS de la
    // fase 3b (los movimientos de la empresa — entradas, traspasos, ajustes — lo dejan en null y solo los ve el staff).
    // Se captura al escribir desde la orden/ticket/activo.
    public Guid? ClientId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? MaintenanceOrderId { get; set; }
    public Guid? ServiceTicketId { get; set; }
    public Guid? TimeLogId { get; set; }

    // Contador de la máquina al cambiar la pieza: con él se mide cuánto duró cada insumo.
    public long? CounterValue { get; set; }

    // Solo tóner: true si se le dejó al usuario para que lo cambie él, false si lo cambió el técnico. null = no aplica.
    public bool? DeliveredToUser { get; set; }

    public string? Notes { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
