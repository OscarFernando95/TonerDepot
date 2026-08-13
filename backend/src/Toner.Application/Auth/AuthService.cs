using Microsoft.EntityFrameworkCore;
using Toner.Application.Auth.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;

namespace Toner.Application.Auth;

public class AuthService : IAuthService
{
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

        if (user is null || !user.IsActive || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return LoginResult.Failure();
        }

        var (token, expiresAtUtc) = _jwtTokenGenerator.GenerateToken(user);
        return LoginResult.Success(token, expiresAtUtc, ToCurrentUserDto(user));
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
