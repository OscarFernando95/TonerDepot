using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<UserService> _logger;

    public UserService(IApplicationDbContext db, IPasswordHasher passwordHasher, ILogger<UserService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
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

        var user = new User
        {
            Cedula = cedula,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(PasswordDefaults.DefaultPassword),
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

        return await ToDtoAsync(user.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await Projected(_db).OrderBy(u => u.FullName).ToListAsync(cancellationToken);
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
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(userId, cancellationToken);
    }

    public async Task<UserDto> ResetPasswordAsync(Guid userId, Guid? performedByUserId = null, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        user.PasswordHash = _passwordHasher.Hash(PasswordDefaults.DefaultPassword);
        user.MustChangePassword = true;
        // Invalida cualquier JWT ya emitido para este usuario (ver SecurityStampValidator).
        user.SecurityStamp = Guid.NewGuid();
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Contraseña reseteada a la genérica para usuario {TargetUserId} por administrador {PerformedByUserId}",
            userId, performedByUserId?.ToString() ?? "desconocido");

        return await ToDtoAsync(userId, cancellationToken);
    }

    private async Task<UserDto> ToDtoAsync(Guid userId, CancellationToken cancellationToken) =>
        await Projected(_db).FirstAsync(u => u.Id == userId, cancellationToken);

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
