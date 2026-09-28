using Toner.Domain.Enums;
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

    // Ubicación del técnico al llegar y al cerrar, y su comparación con la sede (ver LocationStatus).
    // Solo se registra y se alerta; nunca bloquea. AccuracyMeters es la precisión que reportó el GPS.
    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }
    public double? CheckInAccuracyMeters { get; set; }
    public double? CheckInDistanceMeters { get; set; }
    public LocationStatus? CheckInLocationStatus { get; set; }

    public double? CheckOutLatitude { get; set; }
    public double? CheckOutLongitude { get; set; }
    public double? CheckOutAccuracyMeters { get; set; }
    public double? CheckOutDistanceMeters { get; set; }
    public LocationStatus? CheckOutLocationStatus { get; set; }
}
