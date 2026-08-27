using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

public class ServiceTicket : BaseEntity
{
    public Guid ClientLocationId { get; set; }
    public ClientLocation ClientLocation { get; set; } = null!;

    // Denormalizado desde ClientLocation.ClientId para que la política RLS pueda comparar una columna
    // propia en vez de resolver un EXISTS contra ClientLocations — sin esto el predicado del Cliente
    // no es sargable y toda consulta degrada a Seq Scan (SECURITY_AUDIT_V2.md fase 3a).
    //
    // Se CAPTURA AL ESCRIBIR, no se deriva en cada lectura: ClientLocation.ClientId es inmutable (una
    // sede pertenece a un cliente para siempre — UpdateClientLocationRequest no expone ClientId), así
    // que capturar y derivar dan el mismo valor por siempre. NOT NULL sin default a propósito: hace
    // imposible insertar un ticket sin decidir a qué cliente pertenece.
    public Guid ClientId { get; set; }

    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }

    // Solo aplican cuando AssetId es nulo — cliente externo cuyo equipo no está catalogado como Asset.
    // El técnico los llena de forma opcional al cerrar el ticket (ver TechnicianCheckInService.CheckOutAsync).
    public string? ExternalAssetBrand { get; set; }
    public string? ExternalAssetModel { get; set; }
    public long? ExternalAssetCounter { get; set; }

    public Guid ReportedByUserId { get; set; }
    public User ReportedByUser { get; set; } = null!;

    public string Description { get; set; } = string.Empty;
    public ServiceTicketStatus Status { get; set; } = ServiceTicketStatus.Abierto;
    public ServiceTicketPriority Priority { get; set; } = ServiceTicketPriority.Media;

    public Guid? TechnicianId { get; set; }
    public Technician? Technician { get; set; }

    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public ICollection<AssignmentHistory> AssignmentHistories { get; set; } = new List<AssignmentHistory>();
    public ICollection<TimeLog> TimeLogs { get; set; } = new List<TimeLog>();
    public ICollection<Evidence> Evidences { get; set; } = new List<Evidence>();
}
