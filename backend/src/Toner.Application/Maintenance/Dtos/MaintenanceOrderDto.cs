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
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
