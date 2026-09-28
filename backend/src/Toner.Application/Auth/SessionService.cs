using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toner.Application.Auth.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Common;
using Toner.Domain.Entities;

namespace Toner.Application.Auth;

public class SessionService : ISessionService
{
    private static readonly string[] KnownClientTypes = { "web", "mobile" };

    private readonly IApplicationDbContext _db;
    private readonly UserSessionOptions _options;
    private readonly ILogger<SessionService> _logger;

    public SessionService(IApplicationDbContext db, IOptions<UserSessionOptions> options, ILogger<SessionService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<UserSession> StartAsync(
        User user, Guid sessionId, DateTime expiresAtUtc, string? clientType, string? ipAddress, string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var isTechnician = user.Role.Name == RoleNames.Tecnico;

        if (isTechnician)
        {
            var open = await _db.UserSessions
                .Where(s => s.UserId == user.Id && s.RevokedAt == null && s.EnforcesSingleSession)
                .ToListAsync(cancellationToken);

            // Una sesión que venció por tiempo nunca se cerró explícitamente: se cierra acá para que no
            // bloquee el login (gana la sesión activa, no una fila muerta).
            foreach (var stale in open.Where(s => s.ExpiresAt <= now))
            {
                stale.RevokedAt = stale.ExpiresAt;
                stale.RevokedReason = "Expirada";
            }

            var active = open.FirstOrDefault(s => s.ExpiresAt > now);
            if (active is not null)
            {
                _logger.LogWarning(
                    "Login rechazado por sesión activa: técnico {UserId} ya tiene la sesión {SessionId} ({ClientType}) abierta desde {ActiveIp}; intento desde {ClientIp}",
                    user.Id, active.Id, active.ClientType, active.IpAddress ?? "desconocida", ipAddress ?? "desconocida");

                throw new ConflictException(
                    $"Ya tienes una sesión activa en {Describe(active.ClientType)}. Ciérrala allí primero, o pide a un administrador que la cierre.");
            }
        }

        var session = new UserSession
        {
            Id = sessionId,
            UserId = user.Id,
            ClientType = Normalize(clientType),
            IpAddress = Truncate(ipAddress, 64),
            UserAgent = Truncate(userAgent, 300),
            IssuedAt = now,
            LastSeenAt = now,
            ExpiresAt = expiresAtUtc,
            EnforcesSingleSession = isTechnician,
            IdleTimeoutMinutes = !isTechnician && _options.IdleTimeoutMinutes > 0 ? _options.IdleTimeoutMinutes : null
        };

        _db.UserSessions.Add(session);
        return session;
    }

    public async Task RevokeAsync(Guid sessionId, Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        var session = await _db.UserSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null, cancellationToken);

        if (session is null)
        {
            return;
        }

        session.RevokedAt = DateTime.UtcNow;
        session.RevokedReason = reason;
    }

    public async Task<int> RevokeAllForUserAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var open = await _db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in open)
        {
            session.RevokedAt = now;
            session.RevokedReason = reason;
        }

        return open.Count;
    }

    public async Task<SessionValidationResult> ValidateAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default)
    {
        var session = await _db.UserSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);

        if (session is null)
        {
            return SessionValidationResult.Unknown;
        }

        if (session.RevokedAt is not null)
        {
            return SessionValidationResult.Revoked;
        }

        var now = DateTime.UtcNow;

        if (session.ExpiresAt <= now)
        {
            return SessionValidationResult.Expired;
        }

        if (session.IdleTimeoutMinutes is { } idleMinutes && session.LastSeenAt.AddMinutes(idleMinutes) <= now)
        {
            session.RevokedAt = now;
            session.RevokedReason = "Inactividad";
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Sesión {SessionId} del usuario {UserId} cerrada por inactividad de {Minutes} minutos",
                session.Id, userId, idleMinutes);

            return SessionValidationResult.IdleTimeout;
        }

        if ((now - session.LastSeenAt).TotalSeconds >= _options.TouchIntervalSeconds)
        {
            session.LastSeenAt = now;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return SessionValidationResult.Valid;
    }

    public async Task<IReadOnlyList<UserSessionDto>> ListForUserAsync(Guid userId, int take = 20, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.IssuedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return sessions.Select(s => new UserSessionDto
        {
            Id = s.Id,
            ClientType = s.ClientType,
            IpAddress = s.IpAddress,
            UserAgent = s.UserAgent,
            IssuedAt = s.IssuedAt,
            ExpiresAt = s.ExpiresAt,
            LastSeenAt = s.LastSeenAt,
            RevokedAt = s.RevokedAt,
            RevokedReason = s.RevokedReason,
            IsActive = s.RevokedAt is null && s.ExpiresAt > now
                && (s.IdleTimeoutMinutes is null || s.LastSeenAt.AddMinutes(s.IdleTimeoutMinutes.Value) > now)
        }).ToList();
    }

    private static string Normalize(string? clientType)
    {
        var value = clientType?.Trim().ToLowerInvariant();
        return value is not null && KnownClientTypes.Contains(value) ? value : "unknown";
    }

    private static string Describe(string clientType) => clientType switch
    {
        "web" => "la web",
        "mobile" => "la app móvil",
        _ => "otro dispositivo"
    };

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
