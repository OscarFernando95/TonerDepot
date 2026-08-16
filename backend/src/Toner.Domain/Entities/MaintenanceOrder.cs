using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

public class MaintenanceOrder : BaseEntity
{
    public Guid MaintenanceScheduleId { get; set; }
    public MaintenanceSchedule MaintenanceSchedule { get; set; } = null!;

    // Denormalizado desde MaintenanceSchedule.AssetId para facilitar consultas del técnico y del dashboard.
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    public MaintenanceOrderStatus Status { get; set; } = MaintenanceOrderStatus.Pendiente;
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Fijados por MaintenanceScheduleEngine al generar la orden. Combinaciones válidas: General+Unidades,
    // General+Insumos, Unidades sola, Insumos sola — nunca Unidades+Insumos juntas, nunca General sola
    // (si General está por vencer, siempre se combina con la regla más próxima entre unidades e insumos).
    public bool IncludesGeneral { get; set; }
    public bool IncludesUnits { get; set; }
    public bool IncludesConsumables { get; set; }

    public Guid? TechnicianId { get; set; }
    public Technician? Technician { get; set; }

    public ICollection<AssignmentHistory> AssignmentHistories { get; set; } = new List<AssignmentHistory>();
    public ICollection<TimeLog> TimeLogs { get; set; } = new List<TimeLog>();
    public ICollection<Evidence> Evidences { get; set; } = new List<Evidence>();
}
