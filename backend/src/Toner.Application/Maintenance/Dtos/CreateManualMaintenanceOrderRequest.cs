namespace Toner.Application.Maintenance.Dtos;

// Mantenimiento a demanda: lo pide el staff para un equipo instalado sin esperar a que el cronograma cruce un
// umbral de contador o fecha. Qué se hace en la visita es libre (cualquier combinación no vacía); al completarla,
// el cronograma solo reinicia lo que la orden incluyó.
public class CreateManualMaintenanceOrderRequest
{
    public Guid AssetId { get; set; }
    public bool IncludesGeneral { get; set; }
    public bool IncludesUnits { get; set; }
    public bool IncludesConsumables { get; set; }

    // Por qué se pide fuera de calendario (falla, solicitud del cliente, etc.).
    public string? Reason { get; set; }
}
