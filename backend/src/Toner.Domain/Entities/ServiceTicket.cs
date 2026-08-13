using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

public class ServiceTicket : BaseEntity
{
    public Guid ClientLocationId { get; set; }
    public ClientLocation ClientLocation { get; set; } = null!;

    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }

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
