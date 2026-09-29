namespace Toner.Application.Tickets.Dtos;

public class ServiceTicketDto
{
    public Guid Id { get; set; }
    public Guid ClientLocationId { get; set; }
    public string ClientLocationName { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? CityName { get; set; }
    public Guid? AssetId { get; set; }
    public string? AssetBrandName { get; set; }
    public string? AssetModel { get; set; }
    public string? AssetSerialNumber { get; set; }
    public string? ExternalAssetBrand { get; set; }
    public string? ExternalAssetModel { get; set; }
    public long? ExternalAssetCounter { get; set; }
    public Guid ReportedByUserId { get; set; }
    public string ReportedByUserName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public Guid? TechnicianId { get; set; }
    public string? TechnicianName { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    // Solo se llenan en el detalle (GetByIdAsync) y solo para staff/técnico: resumen del trabajo del
    // técnico al cerrar (TimeLog.Notes) y suma de minutos de sus visitas cerradas.
    public string? ResolutionNotes { get; set; }
    public int? ResolutionDurationMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
}
