using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

public class Asset : BaseEntity
{
    // La marca se alcanza vía AssetModel.AssetBrand — un activo solo elige de la lista de modelos
    // de la marca seleccionada, nunca marca y modelo por separado.
    public Guid AssetModelId { get; set; }
    public AssetModel AssetModel { get; set; } = null!;

    public string SerialNumber { get; set; } = string.Empty;
    public AssetType Type { get; set; } = AssetType.Impresora;
    public AssetLifecycleStatus LifecycleStatus { get; set; } = AssetLifecycleStatus.EnBodega;

    // Nula mientras el activo está en bodega o dado de baja.
    public Guid? CurrentClientLocationId { get; set; }
    public ClientLocation? CurrentClientLocation { get; set; }

    // Denormalizado desde CurrentClientLocation.ClientId para que la política RLS compare una columna
    // propia en vez de resolver un EXISTS (SECURITY_AUDIT_V2.md fase 3a).
    //
    // A diferencia de ServiceTicket.ClientId, este NO se captura al escribir: es un ESTADO ACTUAL
    // ("quién tiene el activo ahora"), no un hecho histórico. CurrentClientLocationId es mutable — un
    // equipo vuelve a bodega y se reinstala en otro cliente — así que la columna tiene que SEGUIR al
    // padre. Lo mantiene sincronizado el trigger assets_sync_client_id (ver la migración
    // AddPhase3aDenormalizedClientId); la aplicación nunca la escribe, por eso está mapeada como
    // generada por la base de datos. Nula cuando el activo está en bodega: no pertenece a nadie.
    public Guid? ClientId { get; set; }

    // Área física dentro de la sede (ej. "Contabilidad", "Recepción"). La ingresa el técnico/staff al
    // confirmar la instalación (transición a Instalado), no antes; se limpia al volver a EnBodega.
    public string? Area { get; set; }

    public ICollection<AssetStatusLog> StatusLogs { get; set; } = new List<AssetStatusLog>();
    public ICollection<ContractAsset> ContractAssets { get; set; } = new List<ContractAsset>();
    public ICollection<MeterReading> MeterReadings { get; set; } = new List<MeterReading>();
    public ICollection<MaintenanceSchedule> MaintenanceSchedules { get; set; } = new List<MaintenanceSchedule>();
    public ICollection<ServiceTicket> ServiceTickets { get; set; } = new List<ServiceTicket>();
    public ICollection<TimeLog> TimeLogs { get; set; } = new List<TimeLog>();
    public ICollection<TechnicianAsset> TechnicianAssets { get; set; } = new List<TechnicianAsset>();
}
