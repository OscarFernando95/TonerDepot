using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Tiempo de atención del técnico. Exactamente uno de ServiceTicketId / MaintenanceOrderId debe estar presente.
public class TimeLog : BaseEntity
{
    public Guid? ServiceTicketId { get; set; }
    public ServiceTicket? ServiceTicket { get; set; }

    public Guid? MaintenanceOrderId { get; set; }
    public MaintenanceOrder? MaintenanceOrder { get; set; }

    public Guid TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;

    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public string? Notes { get; set; }
}
