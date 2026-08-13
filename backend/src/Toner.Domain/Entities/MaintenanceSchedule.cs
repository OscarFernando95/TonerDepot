using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

public class MaintenanceSchedule : BaseEntity
{
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    public Guid? ContractId { get; set; }
    public Contract? Contract { get; set; }

    public MaintenanceFrequencyType FrequencyType { get; set; }

    // Aplica según FrequencyType: PorContador usa PrintThreshold, PorTiempo usa TimeIntervalDays.
    public int? PrintThreshold { get; set; }
    public int? TimeIntervalDays { get; set; }

    public DateTime? LastExecutedAt { get; set; }
    public long? LastExecutedCounter { get; set; }

    public DateTime? NextDueAt { get; set; }
    public long? NextDueCounter { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<MaintenanceOrder> MaintenanceOrders { get; set; } = new List<MaintenanceOrder>();
}
