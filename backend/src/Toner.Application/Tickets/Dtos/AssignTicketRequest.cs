namespace Toner.Application.Tickets.Dtos;

public class AssignTicketRequest
{
    public Guid TechnicianId { get; set; }
    public string? Reason { get; set; }
}
