using Microsoft.EntityFrameworkCore;
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

    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(IApplicationDbContext db, IPasswordHasher passwordHasher, IJwtTokenGenerator jwtTokenGenerator)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var cedula = request.Cedula.Trim();

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Technician)
            .FirstOrDefaultAsync(u => u.Cedula == cedula, cancellationToken);

        var userExistsAndActive = user is not null && user.IsActive;

        // Se llama a Verify() en ambas ramas (contra el hash real o contra el señuelo) para que el
        // costo de CPU sea el mismo exista o no el usuario — nunca se hace un short-circuit que se
        // salte el hashing.
        var passwordMatches = _passwordHasher.Verify(request.Password, userExistsAndActive ? user!.PasswordHash : DecoyPasswordHash);

        if (!userExistsAndActive || !passwordMatches)
        {
            return LoginResult.Failure();
        }

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

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        await _db.SaveChangesAsync(cancellationToken);
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
