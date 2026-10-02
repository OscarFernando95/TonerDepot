namespace Toner.Domain.Enums;

public enum InventoryCategory
{
    // Piezas que se revisan/cambian en cada cambio de consumibles (fusor, cilindro, revelador...).
    ConsumibleBase = 0,
    // Piezas opcionales (empaques, extras) que se pueden cambiar en un mantenimiento, ticket o cambio de consumibles.
    Repuesto = 1,
    Toner = 2
}

public enum InventoryLocationKind
{
    // La bodega de la sede principal de la empresa: ahí entran las compras.
    Principal = 0,
    // El inventario de una zona de cobertura: es el que se descuenta al consumir en los municipios de esa zona.
    Zona = 1
}

public enum InventoryMovementType
{
    Entrada = 0,
    TraspasoSalida = 1,
    TraspasoEntrada = 2,
    Consumo = 3,
    Ajuste = 4
}
