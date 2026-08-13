namespace Toner.Application.Maintenance.Dtos;

public class MaintenanceScheduleDto
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public string AssetBrandName { get; set; } = string.Empty;
    public string AssetModel { get; set; } = string.Empty;
    public string AssetSerialNumber { get; set; } = string.Empty;
    public Guid? ContractId { get; set; }
    public string FrequencyType { get; set; } = string.Empty;
    public int? PrintThreshold { get; set; }
    public int? TimeIntervalDays { get; set; }
    public DateTime? LastExecutedAt { get; set; }
    public long? LastExecutedCounter { get; set; }
    public DateTime? NextDueAt { get; set; }
    public long? NextDueCounter { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
