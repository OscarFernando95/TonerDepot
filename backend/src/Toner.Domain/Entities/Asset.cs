using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

public class Asset : BaseEntity
{
    public Guid AssetBrandId { get; set; }
    public AssetBrand AssetBrand { get; set; } = null!;

    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public AssetType Type { get; set; } = AssetType.Impresora;
    public AssetLifecycleStatus LifecycleStatus { get; set; } = AssetLifecycleStatus.EnBodega;

    // Nula mientras el activo está en bodega o dado de baja.
    public Guid? CurrentClientLocationId { get; set; }
    public ClientLocation? CurrentClientLocation { get; set; }

    public ICollection<AssetStatusLog> StatusLogs { get; set; } = new List<AssetStatusLog>();
    public ICollection<ContractAsset> ContractAssets { get; set; } = new List<ContractAsset>();
    public ICollection<MeterReading> MeterReadings { get; set; } = new List<MeterReading>();
    public ICollection<MaintenanceSchedule> MaintenanceSchedules { get; set; } = new List<MaintenanceSchedule>();
    public ICollection<ServiceTicket> ServiceTickets { get; set; } = new List<ServiceTicket>();
}
