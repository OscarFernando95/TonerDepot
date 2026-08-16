namespace Toner.Application.Maintenance.Dtos;

public class MaintenanceScheduleDto
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public string AssetBrandName { get; set; } = string.Empty;
    public string AssetModel { get; set; } = string.Empty;
    public string AssetSerialNumber { get; set; } = string.Empty;

    public Guid ContractId { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? ClientLocationName { get; set; }
    public string? CityName { get; set; }
    public string? Area { get; set; }

    public bool IsActive { get; set; }
    public long? LastKnownCounter { get; set; }

    // Resumen del mantenimiento más reciente (el máximo entre las 3 fechas de abajo) — fecha +
    // glosas (MG/MU/CI) de lo que se hizo en esa misma fecha. Puede ser null si el activo nunca tuvo
    // ningún mantenimiento registrado.
    public DateTime? LastMaintenanceAt { get; set; }
    public List<string> LastMaintenanceCodes { get; set; } = new();

    // Predicción de "qué sigue" (ver MaintenanceComboCalculator) — fecha estimada (null si la regla
    // líder es Insumos solo, que es puro contador), contador estimado, y glosas de qué incluiría.
    public DateTime? NextMaintenanceAt { get; set; }
    public long NextMaintenanceCounter { get; set; }
    public List<string> NextMaintenanceCodes { get; set; } = new();

    // Campos granulares por sub-regla — ya no se muestran como columnas propias, quedan para el detalle
    // del tooltip en la UI.
    public DateTime? LastGeneralMaintenanceAt { get; set; }
    public long? LastGeneralMaintenanceCounter { get; set; }
    public DateTime NextGeneralDueAt { get; set; }
    public long NextGeneralDueCounter { get; set; }

    public DateTime? LastUnitsMaintenanceAt { get; set; }
    public long? LastUnitsMaintenanceCounter { get; set; }
    public DateTime NextUnitsDueAt { get; set; }
    public long NextUnitsDueCounter { get; set; }

    public DateTime? LastConsumablesChangeAt { get; set; }
    public long? LastConsumablesChangeCounter { get; set; }
    public long NextConsumablesDueCounter { get; set; }

    public DateTime CreatedAt { get; set; }
}
