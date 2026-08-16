namespace Toner.Application.Technicians.Dtos;

public class CheckInRequest
{
    // Exactamente uno de los tres debe venir.
    public Guid? ServiceTicketId { get; set; }
    public Guid? MaintenanceOrderId { get; set; }
    public Guid? AssetId { get; set; }
}
