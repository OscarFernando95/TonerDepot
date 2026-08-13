namespace Toner.Application.Maintenance.Dtos;

public class AssignMaintenanceOrderRequest
{
    public Guid TechnicianId { get; set; }
    public string? Reason { get; set; }
}
