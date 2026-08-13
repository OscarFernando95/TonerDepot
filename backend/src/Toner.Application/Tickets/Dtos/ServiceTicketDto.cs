namespace Toner.Application.Tickets.Dtos;

public class ServiceTicketDto
{
    public Guid Id { get; set; }
    public Guid ClientLocationId { get; set; }
    public string ClientLocationName { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public Guid? AssetId { get; set; }
    public string? AssetBrandName { get; set; }
    public string? AssetModel { get; set; }
    public string? AssetSerialNumber { get; set; }
    public Guid ReportedByUserId { get; set; }
    public string ReportedByUserName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public Guid? TechnicianId { get; set; }
    public string? TechnicianName { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
