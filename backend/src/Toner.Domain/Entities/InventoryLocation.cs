using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

// Dónde hay stock: la bodega principal (una sola) o el inventario de una zona (una por zona, se crea con ella).
public class InventoryLocation : BaseEntity
{
    public InventoryLocationKind Kind { get; set; }

    // Solo la principal lleva nombre/dirección/ciudad propios; la de una zona toma el nombre de la zona.
    public string? Name { get; set; }
    public string? Address { get; set; }
    public Guid? CityId { get; set; }
    public City? City { get; set; }

    public Guid? ZoneId { get; set; }
    public Zone? Zone { get; set; }
}
