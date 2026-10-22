namespace Toner.Application.Maintenance.Dtos;

public class MaintenanceOrderCommentDto
{
    public Guid Id { get; set; }
    public Guid MaintenanceOrderId { get; set; }

    // "Comentario" | "Programacion" (ver OrderCommentKind)
    public string Kind { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    // Solo en las programaciones: la fecha y hora (UTC) que fijó el técnico.
    public DateTime? PlannedAt { get; set; }

    public Guid AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    // Cliente | Tecnico | Administrador | Coordinador, tal como era al escribir.
    public string AuthorRole { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
