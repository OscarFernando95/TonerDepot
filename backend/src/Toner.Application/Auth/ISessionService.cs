using Toner.Application.Auth.Dtos;
using Toner.Domain.Entities;

namespace Toner.Application.Auth;

public enum SessionValidationResult
{
    Valid,
    Unknown,
    Revoked,
    Expired,
    IdleTimeout
}

public interface ISessionService
{
    // Sin SaveChangesAsync (salvo ValidateAsync): quien orquesta la operación de negocio persiste una
    // sola vez. Requiere el DbContext scoped compartido, igual que ContractAssetService.
    //
    // deviceId: identificador de instalación que declara la app móvil. Si el técnico ya tiene una sesión viva y es del
    // MISMO dispositivo (típico: reinstaló la app y perdió su token), esa sesión se reemplaza; si es de otro, el login
    // se rechaza como siempre.
    Task<UserSession> StartAsync(
        User user, Guid sessionId, DateTime expiresAtUtc, string? clientType, string? ipAddress, string? userAgent,
        string? deviceId = null, CancellationToken cancellationToken = default);

    Task RevokeAsync(Guid sessionId, Guid userId, string reason, CancellationToken cancellationToken = default);

    Task<int> RevokeAllForUserAsync(Guid userId, string reason, CancellationToken cancellationToken = default);

    // Es su propia operación: además de validar, refresca LastSeenAt (acotado por TouchIntervalSeconds)
    // y marca como revocada la sesión que excedió su inactividad, y persiste eso.
    Task<SessionValidationResult> ValidateAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSessionDto>> ListForUserAsync(Guid userId, int take = 20, CancellationToken cancellationToken = default);
}
