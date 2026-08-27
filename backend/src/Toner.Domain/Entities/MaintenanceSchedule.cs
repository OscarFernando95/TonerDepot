using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Un cronograma por activo (no por regla): cada activo instalado bajo un contrato lleva tres reglas
// independientes y simultáneas — mantenimiento general, mantenimiento de unidades (ambas híbridas
// contador/tiempo, lo que ocurra primero) y cambio de insumos (puro contador) — evaluadas por
// MaintenanceScheduleEngine contra los umbrales de Asset.AssetModel.
public class MaintenanceSchedule : BaseEntity
{
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    // Contrato bajo el cual se creó/renovó el cronograma (informativo — un activo que vuelve a bodega y
    // se reinstala bajo otro contrato reinicia su cronograma con el contrato nuevo).
    public Guid ContractId { get; set; }

    // Denormalizado para la política RLS (fase 3b). CAPTURA AL ESCRIBIR desde Contract.ClientId, que
    // es inmutable (UpdateContractRequest no expone ClientId), así que no puede desincronizarse.
    // NOT NULL: un cronograma siempre se crea bajo un contrato.
    public Guid ClientId { get; set; }
    public Contract Contract { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    // Mantenimiento general
    public DateTime? LastGeneralMaintenanceAt { get; set; }
    public long? LastGeneralMaintenanceCounter { get; set; }
    public DateTime NextGeneralDueAt { get; set; }
    public long NextGeneralDueCounter { get; set; }

    // Mantenimiento de unidades
    public DateTime? LastUnitsMaintenanceAt { get; set; }
    public long? LastUnitsMaintenanceCounter { get; set; }
    public DateTime NextUnitsDueAt { get; set; }
    public long NextUnitsDueCounter { get; set; }

    // Cambio de insumos
    public DateTime? LastConsumablesChangeAt { get; set; }
    public long? LastConsumablesChangeCounter { get; set; }
    public long NextConsumablesDueCounter { get; set; }

    public ICollection<MaintenanceOrder> MaintenanceOrders { get; set; } = new List<MaintenanceOrder>();
}
