using Microsoft.EntityFrameworkCore;
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

    public UserService(IApplicationDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
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
                Status = TechnicianStatus.Inactivo,
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

    public async Task<UserDto> SetActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        user.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(userId, cancellationToken);
    }

    public async Task<UserDto> ResetPasswordAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        user.PasswordHash = _passwordHasher.Hash(PasswordDefaults.DefaultPassword);
        user.MustChangePassword = true;
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(userId, cancellationToken);
    }

    private async Task<UserDto> ToDtoAsync(Guid userId, CancellationToken cancellationToken) =>
        await Projected(_db).FirstAsync(u => u.Id == userId, cancellationToken);

    private static IQueryable<UserDto> Projected(IApplicationDbContext db) =>
        db.Users
            .Include(u => u.Role)
            .Include(u => u.Technician)
            .Include(u => u.City)
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
