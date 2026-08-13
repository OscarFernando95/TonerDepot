using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

public class Contract : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Activo;

    public int? IncludedPrintsPerMonth { get; set; }
    public decimal? PricePerExtraPage { get; set; }
    public string? Notes { get; set; }

    public ICollection<ContractAsset> ContractAssets { get; set; } = new List<ContractAsset>();
    public ICollection<MaintenanceSchedule> MaintenanceSchedules { get; set; } = new List<MaintenanceSchedule>();
}
