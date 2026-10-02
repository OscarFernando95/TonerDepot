namespace Toner.Application.Maintenance.Dtos;

public class MaintenanceOrderDto
{
    public Guid Id { get; set; }
    public Guid MaintenanceScheduleId { get; set; }
    public Guid AssetId { get; set; }
    public string AssetBrandName { get; set; } = string.Empty;
    public string AssetModel { get; set; } = string.Empty;
    public string AssetSerialNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? TechnicianId { get; set; }
    public string? TechnicianName { get; set; }
    public string? ClientLocationName { get; set; }
    public string? CityName { get; set; }
    public bool IncludesGeneral { get; set; }
    public bool IncludesUnits { get; set; }
    public bool IncludesConsumables { get; set; }

    // Pedida a demanda por el staff (no por umbral del cronograma), con el motivo que dio.
    public bool IsManual { get; set; }
    public string? Reason { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
