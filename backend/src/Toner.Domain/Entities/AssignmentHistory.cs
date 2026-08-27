using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

// Registra cada asignación (o intento fallido) de técnico, tanto para ServiceTicket como para MaintenanceOrder.
// Exactamente uno de ServiceTicketId / MaintenanceOrderId debe estar presente (se valida en Application).
public class AssignmentHistory : BaseEntity
{
    // Denormalizado para la política RLS (fase 3b). CAPTURA AL ESCRIBIR desde el ticket o la orden
    // a la que pertenece el intento de asignación. NOT NULL: siempre hay exactamente uno de los dos.
    public Guid ClientId { get; set; }

    public Guid? ServiceTicketId { get; set; }
    public ServiceTicket? ServiceTicket { get; set; }

    public Guid? MaintenanceOrderId { get; set; }
    public MaintenanceOrder? MaintenanceOrder { get; set; }

    // Nulo cuando el resultado del intento de asignación fue "Sin asignar".
    public Guid? TechnicianId { get; set; }
    public Technician? Technician { get; set; }

    // Nulo cuando la asignación la hizo el motor automático en vez de un coordinador.
    public Guid? AssignedByUserId { get; set; }
    public User? AssignedByUser { get; set; }

    public AssignmentType AssignmentType { get; set; }
    public string? Reason { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
