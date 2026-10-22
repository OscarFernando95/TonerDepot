using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

// Hilo de una orden de mantenimiento entre el cliente, el técnico y el staff: el cliente avisa que la fecha programada no le
// sirve, el técnico reprograma. Las programaciones también quedan aquí (Kind = Programacion) para que el hilo cuente la
// historia completa. Nada de esto es interno: lo ven todos los que pueden ver la orden.
//
// ClientId se CAPTURA AL ESCRIBIR desde la orden (MaintenanceOrder.ClientId), para que la política RLS compare una columna
// propia y sargable, igual que MaintenanceOrder y ServiceTicket.
public class MaintenanceOrderComment : BaseEntity
{
    public Guid MaintenanceOrderId { get; set; }
    public MaintenanceOrder MaintenanceOrder { get; set; } = null!;

    public Guid ClientId { get; set; }

    public Guid AuthorUserId { get; set; }
    public User AuthorUser { get; set; } = null!;

    // Rol del autor al escribir (Cliente | Tecnico | Administrador | Coordinador): se guarda porque el rol de un usuario
    // puede cambiar y el hilo debe seguir mostrando quién habló en qué calidad.
    public string AuthorRole { get; set; } = string.Empty;

    public OrderCommentKind Kind { get; set; } = OrderCommentKind.Comentario;

    // Texto del comentario; vacío en las programaciones.
    public string Body { get; set; } = string.Empty;

    // Solo en Kind = Programacion: la fecha y hora (UTC) que el técnico fijó.
    public DateTime? PlannedAt { get; set; }
}
