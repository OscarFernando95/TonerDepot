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
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailTaken = await _db.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (emailTaken)
        {
            throw new ConflictException($"Ya existe un usuario con el correo '{normalizedEmail}'.");
        }

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == request.RoleName, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), request.RoleName);

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
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
        return await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Technician)
            .OrderBy(u => u.FullName)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                RoleName = u.Role.Name,
                IsActive = u.IsActive,
                ClientId = u.ClientId,
                TechnicianId = u.Technician != null ? u.Technician.Id : null,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<UserDto> SetActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        user.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(userId, cancellationToken);
    }

    private async Task<UserDto> ToDtoAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Technician)
            .Where(u => u.Id == userId)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                RoleName = u.Role.Name,
                IsActive = u.IsActive,
                ClientId = u.ClientId,
                TechnicianId = u.Technician != null ? u.Technician.Id : null,
                CreatedAt = u.CreatedAt
            })
            .FirstAsync(cancellationToken);
    }
}
