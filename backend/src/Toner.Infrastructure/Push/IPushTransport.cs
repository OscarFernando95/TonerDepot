using Toner.Application.Push;
using Toner.Domain.Enums;

namespace Toner.Infrastructure.Push;

public sealed record PushTarget(string Token, PushPlatform Platform);

public enum PushOutcome
{
    Sent,
    // FCM dice que el token ya no existe (app desinstalada, token rotado): se borra de la base.
    InvalidToken,
    Failed
}

// Frontera con el proveedor de push (FCM). Separada para poder probar la lógica de destinatarios sin red.
public interface IPushTransport
{
    // false cuando no hay credenciales configuradas: las notificaciones se omiten (con un aviso en el log).
    bool IsEnabled { get; }

    // Un resultado por destino, en el mismo orden.
    Task<IReadOnlyList<PushOutcome>> SendAsync(IReadOnlyList<PushTarget> targets, PushMessage message, CancellationToken cancellationToken = default);
}
