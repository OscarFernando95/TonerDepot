namespace Toner.Application.Tickets.Dtos;

public class SetTicketStatusRequest
{
    // Nombre del enum Toner.Domain.Enums.ServiceTicketStatus.
    public string Status { get; set; } = string.Empty;
}
