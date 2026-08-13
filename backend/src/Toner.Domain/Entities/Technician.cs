using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

public class Technician : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    // Estado actual, denormalizado para que el motor de asignación filtre sin recorrer el historial.
    // Solo lo mueven check-in/check-out y transiciones automáticas del sistema, nunca selección manual del técnico.
    public TechnicianStatus Status { get; set; } = TechnicianStatus.Inactivo;

    public ICollection<TechnicianCoverage> Coverages { get; set; } = new List<TechnicianCoverage>();
    public ICollection<TechnicianAvailability> AvailabilityHistory { get; set; } = new List<TechnicianAvailability>();
    public ICollection<ServiceTicket> AssignedTickets { get; set; } = new List<ServiceTicket>();
    public ICollection<MaintenanceOrder> AssignedMaintenanceOrders { get; set; } = new List<MaintenanceOrder>();
    public ICollection<TimeLog> TimeLogs { get; set; } = new List<TimeLog>();
}
