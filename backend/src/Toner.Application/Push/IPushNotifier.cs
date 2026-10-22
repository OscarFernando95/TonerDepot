namespace Toner.Application.Push;

// Notificación push. Quien la construye pone un Title/Body genérico y en Data SOLO identificadores (type, id, event); el
// notificador completa el Body con el resumen operativo del servicio (cliente y ciudad, criticidad, equipo y área) justo
// antes de enviar. Ese resumen puede verse en la pantalla de bloqueo, así que nunca lleva direcciones, contactos ni
// descripciones, y se envía con visibilidad "privada" de Android (los teléfonos configurados para ocultar el contenido
// sensible lo ocultan). El detalle completo se consulta por la API con la sesión del usuario cuando abre la notificación.
public sealed record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string> Data);

public interface IPushNotifier
{
    // A los usuarios de esos técnicos (los técnicos se identifican por Technician.Id).
    Task NotifyTechniciansAsync(IReadOnlyCollection<Guid> technicianIds, PushMessage message, CancellationToken cancellationToken = default);

    // A todo el staff activo (Administrador y Coordinador).
    Task NotifyStaffAsync(PushMessage message, CancellationToken cancellationToken = default);

    // Al técnico asignado a una orden de mantenimiento; si la orden aún no tiene técnico, al staff (alguien debe enterarse).
    Task NotifyOrderTechnicianAsync(Guid orderId, PushMessage message, CancellationToken cancellationToken = default);

    // Una instalación pendiente no se asigna: la puede tomar cualquier técnico que cubra el municipio de su sede. Se
    // avisa a esos técnicos (los mismos que la ven en su lista de instalaciones pendientes).
    Task NotifyInstallersAsync(Guid assetId, PushMessage message, CancellationToken cancellationToken = default);
}
