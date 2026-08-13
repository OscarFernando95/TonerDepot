using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Common;
using Toner.Domain.Entities;

namespace Toner.Infrastructure.Persistence.Seed;

// Siembra los 5 roles fijos del sistema, el catálogo de ciudades de Colombia, y si aún no existe
// ningún Administrador, crea uno de arranque a partir de configuración (para poder entrar la primera vez).
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

        await SeedCitiesAsync(cancellationToken);

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
            // Fijo a pedido: es la cuenta de arranque, no un usuario creado desde el módulo de Usuarios.
            Cedula = "1234567890",
            Email = adminEmail.Trim().ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(adminPassword),
            // Su contraseña viene de configuración explícita (AdminBootstrap:Password), no de la
            // genérica — no aplica forzar cambio como a los usuarios creados desde la UI.
            MustChangePassword = false,
            FullName = adminFullName,
            RoleId = adminRole.Id,
            IsActive = true
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    // Ciudad+departamento son ahora dato de referencia fijo (ya no hay UI para crear ciudades a mano),
    // sembrado una sola vez desde el JSON embebido y luego siempre idempotente vía upsert por
    // (Name, StateOrProvince) — no duplica lo ya sembrado ni toca ciudades creadas por otra vía.
    private async Task SeedCitiesAsync(CancellationToken cancellationToken)
    {
        const string resourceName = "Toner.Infrastructure.Persistence.Seed.Data.colombia-cities.json";
        var assembly = typeof(DataSeeder).Assembly;

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Recurso embebido '{resourceName}' no encontrado. Disponibles: {string.Join(", ", assembly.GetManifestResourceNames())}");

        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var departments = await JsonSerializer.DeserializeAsync<List<DepartmentSeedDto>>(stream, jsonOptions, cancellationToken)
            ?? new List<DepartmentSeedDto>();

        var existing = (await _db.Cities
                .Select(c => new { c.Name, c.StateOrProvince })
                .ToListAsync(cancellationToken))
            .Select(e => (e.Name, e.StateOrProvince))
            .ToHashSet();

        foreach (var department in departments)
        {
            var stateOrProvince = department.Departamento.Trim();
            foreach (var cityName in department.Ciudades)
            {
                var name = cityName.Trim();
                if (existing.Contains((name, stateOrProvince)))
                {
                    continue;
                }

                _db.Cities.Add(new City { Name = name, StateOrProvince = stateOrProvince });
                existing.Add((name, stateOrProvince));
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private sealed class DepartmentSeedDto
    {
        public string Departamento { get; set; } = string.Empty;
        public List<string> Ciudades { get; set; } = new();
    }
}
