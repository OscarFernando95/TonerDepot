using Toner.Application.Push.Dtos;

namespace Toner.Application.Push;

public interface IDeviceTokenService
{
    // Registra (o reemplaza) el token del dispositivo del usuario para esa plataforma.
    Task RegisterAsync(Guid userId, RegisterDeviceTokenRequest request, CancellationToken cancellationToken = default);

    // El usuario deja de querer notificaciones en esa plataforma.
    Task UnregisterAsync(Guid userId, string platform, CancellationToken cancellationToken = default);
}
