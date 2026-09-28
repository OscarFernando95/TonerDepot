namespace Toner.Application.Evidences.Dtos;

public class EvidenceDto
{
    public Guid Id { get; set; }
    // "Antes" | "Despues"
    public string Kind { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public Guid? ServiceTicketId { get; set; }
    public Guid? MaintenanceOrderId { get; set; }
    public Guid? TimeLogId { get; set; }
}
