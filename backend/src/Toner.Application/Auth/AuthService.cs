using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Toner.Application.Auth.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;

namespace Toner.Application.Auth;

public class AuthService : IAuthService
{
    // Hash BCrypt (workFactor 12, igual que BCryptPasswordHasher) de un valor arbitrario que no
    // corresponde a ninguna cuenta real. Se usa solo para que Verify() tarde lo mismo cuando el
    // usuario no existe o está inactivo — si ahí se saltara el hashing, la diferencia de tiempo entre
    // "cédula inexistente" (~5ms) y "cédula válida, contraseña incorrecta" (~300ms) permitiría
    // enumerar cédulas válidas por canal lateral aunque el mensaje de error sea siempre el mismo
    // (ver SECURITY_AUDIT.md hallazgo #7).
    private const string DecoyPasswordHash = "$2a$12$IU/4mDLs28Px7KGoaSZ/JO.n7wIaeVFmdkqOZyfAeUgoJRL8vwaty";

    // Bloqueo de cuenta por intentos fallidos consecutivos (ver SECURITY_AUDIT.md hallazgo #9).
    // Control adicional por CUENTA, complementario al rate limiting por IP del hallazgo #2 — protege
    // contra un atacante que rota de IP pero insiste sobre la misma cédula.
    // Debe coincidir con UserSessionConfiguration.SingleActiveSessionIndexName (Application no referencia Infrastructure).
    private const string SingleActiveSessionIndexName = "IX_UserSessions_UserId_SingleActive";

    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ILogger<AuthService> _logger;
    private readonly ISessionService _sessions;

    public AuthService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ILogger<AuthService> logger,
        ISessionService sessions)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _logger = logger;
        _sessions = sessions;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, string? ipAddress = null, CancellationToken cancellationToken = default, string? clientType = null, string? userAgent = null)
    {
        var cedula = request.Cedula.Trim();

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Technician)
            .FirstOrDefaultAsync(u => u.Cedula == cedula, cancellationToken);

        var now = DateTime.UtcNow;
        var isLockedOut = user is not null && user.LockedOutUntil.HasValue && user.LockedOutUntil.Value > now;

        // Al expirar el bloqueo, el contador vuelve a cero (SECURITY_AUDIT_V2.md hallazgo N2). Sin
        // esto, FailedLoginAttempts quedaba en 5 después del bloqueo, así que UN solo intento fallido
        // posterior re-bloqueaba la cuenta otros 15 minutos: alguien que conociera la cédula podía
        // mantenerla bloqueada indefinidamente con 1 request cada 15 min, muy por debajo del rate
        // limit y sin saber la contraseña. Con el reinicio, sostener el ataque cuesta 5 intentos por
        // ventana en vez de 1 — tráfico continuo y visible en los logs de login fallido.
        //
        // El bloqueo por intentos fallidos es inherentemente abusable por quien conozca al usuario
        // objetivo: es un intercambio entre frenar la fuerza bruta y permitir que un tercero deje a
        // alguien fuera. Esto no lo elimina, solo sube el costo del abuso sin debilitar el control.
        var lockoutJustExpired = user is not null && user.LockedOutUntil.HasValue && !isLockedOut;
        if (lockoutJustExpired)
        {
            user!.FailedLoginAttempts = 0;
            user.LockedOutUntil = null;
        }

        var userExistsAndActive = user is not null && user.IsActive && !isLockedOut;

        // Se llama a Verify() en todas las ramas (contra el hash real o contra el señuelo) para que
        // el costo de CPU sea el mismo exista, esté activo, o esté bloqueado el usuario — nunca se
        // hace un short-circuit que se salte el hashing. Una cuenta bloqueada NUNCA se compara contra
        // su PasswordHash real: entra directo al mismo camino que "usuario no existe".
        var passwordMatches = _passwordHasher.Verify(request.Password, userExistsAndActive ? user!.PasswordHash : DecoyPasswordHash);

        if (!userExistsAndActive || !passwordMatches)
        {
            var reason = user is null
                ? "UsuarioNoExiste"
                : !user.IsActive
                    ? "UsuarioInactivo"
                    : isLockedOut
                        ? "CuentaBloqueada"
                        : "ContraseñaIncorrecta";

            // El contador solo avanza cuando la causa real es contraseña incorrecta sobre una cuenta
            // activa y no bloqueada — nunca por cuenta inexistente, inactiva, o ya bloqueada (si no,
            // seguir insistiendo durante el bloqueo lo extendería indefinidamente). Este es el único
            // branch con un SaveChangesAsync real (UPDATE) — una asimetría de unos pocos ms frente a
            // UsuarioNoExiste/CuentaBloqueada, mucho menor que los ~295ms que cerró el fix #7, y sobre
            // una rama que ya era la "cara" por diseño desde ese mismo fix. No se iguala a propósito.
            if (reason == "ContraseñaIncorrecta")
            {
                user!.FailedLoginAttempts++;

                if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
                {
                    user.LockedOutUntil = now.Add(LockoutDuration);

                    _logger.LogWarning(
                        "Cuenta bloqueada por {Minutes} minutos tras {Attempts} intentos fallidos consecutivos: usuario {UserId} (cédula {Cedula})",
                        LockoutDuration.TotalMinutes, user.FailedLoginAttempts, user.Id, cedula);
                }

                await _db.SaveChangesAsync(cancellationToken);
            }

            _logger.LogWarning(
                "Login fallido para cédula {Cedula} desde IP {IpAddress}. Motivo: {Reason}",
                cedula, ipAddress ?? "desconocida", reason);

            return LoginResult.Failure();
        }

        // lockoutJustExpired entra en la condición a propósito: en ese caso los campos ya se pusieron
        // en cero más arriba, pero SOLO en memoria — sin incluirlo acá, el SaveChanges se saltaría y
        // la fila quedaría con los valores viejos en base de datos.
        if (lockoutJustExpired || user!.FailedLoginAttempts != 0 || user.LockedOutUntil.HasValue)
        {
            user!.FailedLoginAttempts = 0;
            user.LockedOutUntil = null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Login exitoso para cédula {Cedula} (usuario {UserId}) desde IP {IpAddress}",
            cedula, user!.Id, ipAddress ?? "desconocida");

        var sessionId = Guid.NewGuid();
        var (token, expiresAtUtc) = _jwtTokenGenerator.GenerateToken(user!, sessionId);

        // Puede lanzar ConflictException (técnico con sesión activa — gana la primera). El contador de
        // intentos fallidos ya se limpió arriba: la contraseña era correcta.
        await _sessions.StartAsync(user!, sessionId, expiresAtUtc, clientType, ipAddress, userAgent, cancellationToken);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains(SingleActiveSessionIndexName) == true)
        {
            // Dos logins simultáneos del mismo técnico pasaron el chequeo a la vez: el índice único
            // parcial deja pasar solo a uno.
            throw new ConflictException("Ya tienes una sesión activa en otro dispositivo. Ciérrala allí primero, o pide a un administrador que la cierre.");
        }

        return LoginResult.Success(token, expiresAtUtc, ToCurrentUserDto(user!));
    }

    public async Task LogoutAsync(Guid userId, Guid sessionId, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        await _sessions.RevokeAsync(sessionId, userId, "Logout", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Logout del usuario {UserId} (sesión {SessionId}) desde IP {IpAddress}",
            userId, sessionId, ipAddress ?? "desconocida");
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Technician)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        return ToCurrentUserDto(user);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            _logger.LogWarning(
                "Cambio de contraseña fallido para usuario {UserId} desde IP {IpAddress}: contraseña actual incorrecta",
                userId, ipAddress ?? "desconocida");

            throw new InvalidCredentialsException();
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        // Invalida cualquier JWT ya emitido para este usuario (ver SecurityStampValidator).
        user.SecurityStamp = Guid.NewGuid();
        // Sin esto la sesión (y, para un técnico, el bloqueo de sesión única) sobreviviría al token muerto.
        await _sessions.RevokeAllForUserAsync(userId, "CambioContraseña", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cambio de contraseña exitoso para usuario {UserId} desde IP {IpAddress}",
            userId, ipAddress ?? "desconocida");
    }

    private static CurrentUserDto ToCurrentUserDto(User user) => new()
    {
        Id = user.Id,
        Cedula = user.Cedula,
        Email = user.Email,
        FullName = user.FullName,
        Role = user.Role.Name,
        ClientId = user.ClientId,
        TechnicianId = user.Technician?.Id,
        MustChangePassword = user.MustChangePassword
    };
}
