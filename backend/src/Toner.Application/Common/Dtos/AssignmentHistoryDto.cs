namespace Toner.Application.Common.Dtos;

// Compartido entre ServiceTicket y MaintenanceOrder: ambos se asignan a través del mismo motor
// y registran su historial en la misma tabla AssignmentHistory.
public class AssignmentHistoryDto
{
    public Guid Id { get; set; }
    public Guid? TechnicianId { get; set; }
    public string? TechnicianName { get; set; }
    public string? AssignedByUserName { get; set; }
    public string AssignmentType { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime AssignedAt { get; set; }
}
