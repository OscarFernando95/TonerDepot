using Toner.Domain.Enums;
using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Evidencia fotográfica subida por el técnico. Exactamente uno de ServiceTicketId / MaintenanceOrderId debe estar presente.
public class Evidence : BaseEntity
{
    // Denormalizado para la política RLS (mismo patrón que TimeLog): se CAPTURA AL ESCRIBIR desde el
    // ticket u orden. NOT NULL: toda evidencia nace de un padre que siempre tiene cliente.
    public Guid ClientId { get; set; }

    public EvidenceKind Kind { get; set; }
    public long SizeBytes { get; set; }

    // El check-in/out al que quedó atada la foto (null mientras solo está subida).
    public Guid? TimeLogId { get; set; }

    public Guid? ServiceTicketId { get; set; }
    public ServiceTicket? ServiceTicket { get; set; }

    public Guid? MaintenanceOrderId { get; set; }
    public MaintenanceOrder? MaintenanceOrder { get; set; }

    // Ruta/clave dentro del contenedor de blobs, no una URL firmada (esa se genera al servir la evidencia).
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;

    public Guid UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
