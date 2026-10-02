namespace Toner.Application.Push;

// Notificación push. Lo visible (Title/Body) es genérico a propósito: puede mostrarse con la pantalla bloqueada y con
// la sesión cerrada, así que nunca lleva nombres de clientes, direcciones ni datos del servicio. Data lleva SOLO
// identificadores; la app consulta el detalle por la API cuando el usuario la abre (y con su sesión).
public sealed record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string> Data);

public interface IPushNotifier
{
    // A los usuarios de esos técnicos (los técnicos se identifican por Technician.Id).
    Task NotifyTechniciansAsync(IReadOnlyCollection<Guid> technicianIds, PushMessage message, CancellationToken cancellationToken = default);

    // A todo el staff activo (Administrador y Coordinador).
    Task NotifyStaffAsync(PushMessage message, CancellationToken cancellationToken = default);
}
