namespace Toner.Application.Technicians.Dtos;

public class TechnicianSelfStatusDto
{
    public Guid TechnicianId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ActiveServiceTicketId { get; set; }
    public Guid? ActiveMaintenanceOrderId { get; set; }
    public DateTime? CheckedInAt { get; set; }
}
