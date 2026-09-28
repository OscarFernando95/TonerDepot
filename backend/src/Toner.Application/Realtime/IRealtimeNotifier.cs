namespace Toner.Application.Realtime;

// Aviso de que algo cambió. Lleva SOLO identificadores, nunca datos: quien lo recibe vuelve a pedir el recurso por
// la API, con su autorización de siempre. Así un cliente no puede recibir por el canal en vivo datos que la API no
// le entregaría (y no hay nada sensible que filtrar si un evento llega a un destinatario equivocado).
//
// Entity: Ticket | MaintenanceOrder | Visit | Technician | Asset | Contract | Client | Evidence | Schedule | User
// Action: created | updated | deleted
public sealed record EntityChange(
    string Entity,
    Guid Id,
    string Action,
    // Cliente dueño del dato (si aplica): sus usuarios reciben el aviso.
    Guid? ClientId,
    // Técnicos involucrados (el actual y, si cambió, el anterior): reciben el aviso.
    IReadOnlyCollection<Guid> TechnicianIds);

public interface IRealtimeNotifier
{
    Task PublishAsync(IReadOnlyList<EntityChange> changes, CancellationToken cancellationToken = default);

    // Las sesiones revocadas (logout, cierre por administrador, cambio de contraseña, desactivación) dejan de
    // recibir avisos y se les notifica, aunque su conexión en vivo siga abierta.
    Task SessionsRevokedAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken = default);
}
