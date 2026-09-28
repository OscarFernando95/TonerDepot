using Microsoft.EntityFrameworkCore;
using Toner.Application.Auth;
using Toner.Application.Common.Paging;
using Microsoft.Extensions.Logging;
using Toner.Application.Auth;
using Toner.Application.Auth.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Users.Dtos;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Users;

public class UserService : IUserService
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IBackgroundJobScheduler _jobScheduler;
    private readonly ILogger<UserService> _logger;
    private readonly ISessionService _sessions;

    public UserService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IBackgroundJobScheduler jobScheduler,
        ILogger<UserService> logger,
        ISessionService sessions)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jobScheduler = jobScheduler;
        _logger = logger;
        _sessions = sessions;
    }

    public async Task<UserWithGeneratedPasswordDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var cedula = request.Cedula.Trim();

        var cedulaTaken = await _db.Users.AnyAsync(u => u.Cedula == cedula, cancellationToken);
        if (cedulaTaken)
        {
            throw new ConflictException($"Ya existe un usuario con la cédula '{cedula}'.");
        }

        var normalizedEmail = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == request.RoleName, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), request.RoleName);

        var generatedPassword = SecurePasswordGenerator.Generate();

        var user = new User
        {
            Cedula = cedula,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(generatedPassword),
            MustChangePassword = true,
            FullName = request.FullName.Trim(),
            Phone = request.Phone.Trim(),
            Address = request.Address.Trim(),
            CityId = request.CityId,
            RoleId = role.Id,
            ClientId = request.RoleName == RoleNames.Cliente ? request.ClientId : null,
            IsActive = true
        };

        _db.Users.Add(user);

        if (request.RoleName == RoleNames.Tecnico)
        {
            _db.Technicians.Add(new Technician
            {
                UserId = user.Id,
                // Disponible desde que se crea: Status solo refleja si está en una visita (Ocupado) o no,
                // no si la cuenta está habilitada (eso es IsActive) — "Inactivo" confundía al leerse como
                // cuenta deshabilitada.
                Status = TechnicianStatus.Disponible,
                IsActive = true
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Después del único SaveChanges: si algo previo falla, no queremos haber encolado un correo
        // para un usuario que no llegó a existir.
        _jobScheduler.EnqueueGeneratedPasswordEmail(cedula, generatedPassword);

        var dto = await ToDtoAsync(user.Id, cancellationToken);
        return ToWithGeneratedPassword(dto, generatedPassword);
    }

    public async Task<PagedResult<UserDto>> ListAsync(int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        return await Projected(_db).OrderBy(u => u.FullName).ToOffsetPageAsync(page, pageSize, cancellationToken);
    }

    public async Task<UserDto> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        var cedula = request.Cedula.Trim();
        var cedulaTakenByAnotherUser = await _db.Users.AnyAsync(u => u.Cedula == cedula && u.Id != userId, cancellationToken);
        if (cedulaTakenByAnotherUser)
        {
            throw new ConflictException($"Ya existe un usuario con la cédula '{cedula}'.");
        }

        user.Cedula = cedula;
        user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        user.FullName = request.FullName.Trim();
        user.Phone = request.Phone.Trim();
        user.Address = request.Address.Trim();
        user.CityId = request.CityId;

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(userId, cancellationToken);
    }

    public async Task<UserDto> SetActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        user.IsActive = isActive;
        // Invalida cualquier JWT ya emitido en ambas direcciones (no solo al desactivar) — más simple
        // que condicionar por dirección, y no tiene downside: nadie tiene un token válido para una
        // cuenta que estaba inactiva, así que regenerar también al reactivar no invalida nada que
        // debiera seguir vivo (ver SecurityStampValidator).
        user.SecurityStamp = Guid.NewGuid();
        await _sessions.RevokeAllForUserAsync(userId, isActive ? "CuentaReactivada" : "CuentaDesactivada", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(userId, cancellationToken);
    }

    public async Task<UserWithGeneratedPasswordDto> ResetPasswordAsync(Guid userId, Guid? performedByUserId = null, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        var generatedPassword = SecurePasswordGenerator.Generate();

        user.PasswordHash = _passwordHasher.Hash(generatedPassword);
        user.MustChangePassword = true;
        // Invalida cualquier JWT ya emitido para este usuario (ver SecurityStampValidator).
        user.SecurityStamp = Guid.NewGuid();
        await _sessions.RevokeAllForUserAsync(userId, "ContraseñaReseteada", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _jobScheduler.EnqueueGeneratedPasswordEmail(user.Cedula, generatedPassword);

        // Nunca la contraseña acá, ni en claro ni cifrada (SECURITY_AUDIT.md hallazgo #8) — solo el
        // hecho de que se reseteó y quién lo pidió.
        _logger.LogInformation(
            "Contraseña reseteada para usuario {TargetUserId} por administrador {PerformedByUserId}",
            userId, performedByUserId?.ToString() ?? "desconocido");

        var dto = await ToDtoAsync(userId, cancellationToken);
        return ToWithGeneratedPassword(dto, generatedPassword);
    }

    public async Task<int> RevokeSessionsAsync(Guid userId, Guid? performedByUserId = null, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException(nameof(User), userId);
        }

        var revoked = await _sessions.RevokeAllForUserAsync(userId, "CerradaPorAdministrador", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Sesiones cerradas por administrador {PerformedByUserId}: usuario {TargetUserId}, {Count} sesión(es)",
            performedByUserId?.ToString() ?? "desconocido", userId, revoked);

        return revoked;
    }

    public async Task<IReadOnlyList<UserSessionDto>> ListSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException(nameof(User), userId);
        }

        return await _sessions.ListForUserAsync(userId, 20, cancellationToken);
    }

    private async Task<UserDto> ToDtoAsync(Guid userId, CancellationToken cancellationToken) =>
        await Projected(_db).FirstAsync(u => u.Id == userId, cancellationToken);

    private static UserWithGeneratedPasswordDto ToWithGeneratedPassword(UserDto dto, string generatedPassword) =>
        new()
        {
            Id = dto.Id,
            Cedula = dto.Cedula,
            Email = dto.Email,
            FullName = dto.FullName,
            Phone = dto.Phone,
            Address = dto.Address,
            CityId = dto.CityId,
            CityName = dto.CityName,
            RoleName = dto.RoleName,
            IsActive = dto.IsActive,
            MustChangePassword = dto.MustChangePassword,
            ClientId = dto.ClientId,
            TechnicianId = dto.TechnicianId,
            CreatedAt = dto.CreatedAt,
            GeneratedPassword = generatedPassword
        };

    private static IQueryable<UserDto> Projected(IApplicationDbContext db) =>
        db.Users
            .Select(u => new UserDto
            {
                Id = u.Id,
                Cedula = u.Cedula,
                Email = u.Email,
                FullName = u.FullName,
                Phone = u.Phone,
                Address = u.Address,
                CityId = u.CityId,
                CityName = u.City != null ? u.City.Name : null,
                RoleName = u.Role.Name,
                IsActive = u.IsActive,
                MustChangePassword = u.MustChangePassword,
                ClientId = u.ClientId,
                TechnicianId = u.Technician != null ? u.Technician.Id : null,
                CreatedAt = u.CreatedAt
            });
}
