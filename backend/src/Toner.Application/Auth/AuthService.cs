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
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _logger = logger;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        var cedula = request.Cedula.Trim();

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Technician)
            .FirstOrDefaultAsync(u => u.Cedula == cedula, cancellationToken);

        var now = DateTime.UtcNow;
        var isLockedOut = user is not null && user.LockedOutUntil.HasValue && user.LockedOutUntil.Value > now;
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

        if (user!.FailedLoginAttempts != 0 || user.LockedOutUntil.HasValue)
        {
            user.FailedLoginAttempts = 0;
            user.LockedOutUntil = null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Login exitoso para cédula {Cedula} (usuario {UserId}) desde IP {IpAddress}",
            cedula, user!.Id, ipAddress ?? "desconocida");

        var (token, expiresAtUtc) = _jwtTokenGenerator.GenerateToken(user!);
        return LoginResult.Success(token, expiresAtUtc, ToCurrentUserDto(user!));
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
