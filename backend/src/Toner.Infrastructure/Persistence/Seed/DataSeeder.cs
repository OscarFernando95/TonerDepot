using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Common;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Seed;

// Siembra los 5 roles fijos del sistema y, si aún no existe ningún Administrador,
// crea uno de arranque a partir de configuración (para poder entrar la primera vez).
public class DataSeeder
{
    private readonly TonerDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IPasswordHasher _passwordHasher;

    public DataSeeder(TonerDbContext db, IConfiguration configuration, IPasswordHasher passwordHasher)
    {
        _db = db;
        _configuration = configuration;
        _passwordHasher = passwordHasher;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existingRoles = await _db.Roles.Select(r => r.Name).ToListAsync(cancellationToken);
        foreach (var roleName in RoleNames.All.Except(existingRoles))
        {
            _db.Roles.Add(new Role { Name = roleName });
        }
        await _db.SaveChangesAsync(cancellationToken);

        var hasAdmin = await _db.Users
            .Include(u => u.Role)
            .AnyAsync(u => u.Role.Name == RoleNames.Administrador, cancellationToken);

        if (hasAdmin)
        {
            return;
        }

        var adminEmail = _configuration["AdminBootstrap:Email"];
        var adminPassword = _configuration["AdminBootstrap:Password"];
        var adminFullName = _configuration["AdminBootstrap:FullName"] ?? "Administrador";

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        var adminRole = await _db.Roles.FirstAsync(r => r.Name == RoleNames.Administrador, cancellationToken);

        _db.Users.Add(new User
        {
            Email = adminEmail.Trim().ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(adminPassword),
            FullName = adminFullName,
            RoleId = adminRole.Id,
            IsActive = true
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
