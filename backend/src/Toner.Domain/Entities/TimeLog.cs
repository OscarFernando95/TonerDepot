using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Tiempo de atención del técnico. Exactamente uno de ServiceTicketId / MaintenanceOrderId / AssetId debe estar presente.
public class TimeLog : BaseEntity
{
    // Denormalizado para la política RLS (fase 3b). CAPTURA AL ESCRIBIR desde el padre presente
    // (ticket, orden o activo). Nullable: el check-in de instalación puede apuntar a un activo sin
    // cliente asignado todavía.
    public Guid? ClientId { get; set; }

    public Guid? ServiceTicketId { get; set; }
    public ServiceTicket? ServiceTicket { get; set; }

    public Guid? MaintenanceOrderId { get; set; }
    public MaintenanceOrder? MaintenanceOrder { get; set; }

    // Presente cuando el check-in es para confirmar la instalación de un activo (AssetLifecycleStatus.PendienteInstalacion).
    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }

    public Guid TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;

    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public string? Notes { get; set; }
}
