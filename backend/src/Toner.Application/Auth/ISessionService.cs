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
    Task<UserSession> StartAsync(
        User user, Guid sessionId, DateTime expiresAtUtc, string? clientType, string? ipAddress, string? userAgent,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(Guid sessionId, Guid userId, string reason, CancellationToken cancellationToken = default);

    Task<int> RevokeAllForUserAsync(Guid userId, string reason, CancellationToken cancellationToken = default);

    // Es su propia operación: además de validar, refresca LastSeenAt (acotado por TouchIntervalSeconds)
    // y marca como revocada la sesión que excedió su inactividad, y persiste eso.
    Task<SessionValidationResult> ValidateAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSessionDto>> ListForUserAsync(Guid userId, int take = 20, CancellationToken cancellationToken = default);
}
