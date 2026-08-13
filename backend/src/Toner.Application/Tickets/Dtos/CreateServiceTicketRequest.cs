namespace Toner.Application.Tickets.Dtos;

public class CreateServiceTicketRequest
{
    public Guid ClientLocationId { get; set; }
    public Guid? AssetId { get; set; }
    public string Description { get; set; } = string.Empty;

    // Nombre del enum Toner.Domain.Enums.ServiceTicketPriority. Si se omite, se asume Media.
    public string? Priority { get; set; }
}
