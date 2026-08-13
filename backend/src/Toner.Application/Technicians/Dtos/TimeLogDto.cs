namespace Toner.Application.Technicians.Dtos;

public class TimeLogDto
{
    public Guid Id { get; set; }
    public Guid? ServiceTicketId { get; set; }
    public Guid? MaintenanceOrderId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Notes { get; set; }
}
