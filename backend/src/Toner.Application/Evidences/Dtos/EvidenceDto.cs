namespace Toner.Application.Evidences.Dtos;

public class EvidenceDto
{
    public Guid Id { get; set; }
    // "Antes" | "Despues" | "Contador"
    public string Kind { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public Guid? ServiceTicketId { get; set; }
    public Guid? MaintenanceOrderId { get; set; }
    public Guid? TimeLogId { get; set; }

    // Si tiene valor, el archivo se borró por la política de retención y ya no puede descargarse; el registro se
    // conserva (AUDIT.md H-44). Los clientes muestran un recuadro en vez de pedir el contenido.
    public DateTime? ContentPurgedAt { get; set; }
}
