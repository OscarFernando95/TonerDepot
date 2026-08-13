using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

// Historial de cambios de estado del técnico (check-in, check-out, inactivación manual, etc.).
public class TechnicianAvailability : BaseEntity
{
    public Guid TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;

    public TechnicianStatus Status { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
}
