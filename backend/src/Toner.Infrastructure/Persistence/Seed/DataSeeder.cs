using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    private readonly IHostEnvironment _environment;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(
        TonerDbContext db,
        IConfiguration configuration,
        IPasswordHasher passwordHasher,
        IHostEnvironment environment,
        ITenantContextAccessor tenantContextAccessor,
        ILogger<DataSeeder> logger)
    {
        _db = db;
        _configuration = configuration;
        _passwordHasher = passwordHasher;
        _environment = environment;
        _tenantContextAccessor = tenantContextAccessor;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Scope de staff explícito: el seeder corre en el arranque, fuera de cualquier request, así
        // que no hay TenantContextMiddleware que establezca el contexto (ver hallazgo #5).
        using var tenantScope = _tenantContextAccessor.Push(() => TenantContext.Staff);

        try
        {
            await SeedCoreAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex.GetType() != typeof(InvalidOperationException))
        {
            // Comparación de tipo exacto (no "is"): los InvalidOperationException que lanzamos
            // nosotros mismos más abajo (config de AdminBootstrap faltante, recurso embebido no
            // encontrado) ya son mensajes deliberados y específicos — se dejan propagar tal cual. Un
            // "is not InvalidOperationException" además excluiría por accidente sus subclases, como
            // ObjectDisposedException, que si son fallos reales que sí queremos envolver abajo.
            // Trade-off del hallazgo #13: la app NO sigue arrancando sin seed. Hangfire usa la misma
            // cadena de conexión, así que si Postgres no responde aquí, tampoco va a arrancar más
            // abajo — "seguir arrancando" solo daría un proceso que reporta healthy pero no puede
            // atender ninguna request real. Mejor fallar el arranque ahora, con un mensaje claro (no
            // el stack trace crudo de Npgsql), que un despliegue silenciosamente roto.
            _logger.LogCritical(ex,
                "No se pudo completar el seed inicial (roles, ciudades, o el admin de arranque). " +
                "La aplicación no va a arrancar.");

            throw new InvalidOperationException(
                "Falló el seed inicial de la base de datos al arrancar. Verifica que Postgres esté " +
                "disponible y sea alcanzable con la configuración actual, y vuelve a intentar.", ex);
        }
    }

    private async Task SeedCoreAsync(CancellationToken cancellationToken)
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

        var adminCedula = _configuration["AdminBootstrap:Cedula"];
        var adminEmail = _configuration["AdminBootstrap:Email"];
        var adminPassword = _configuration["AdminBootstrap:Password"];
        var adminFullName = _configuration["AdminBootstrap:FullName"] ?? "Administrador";

        if (string.IsNullOrWhiteSpace(adminCedula) || string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            // En producción arrancar sin poder crear el admin de arranque deja el sistema sin forma
            // de entrar la primera vez — mejor un fallo explícito en el arranque que un despliegue
            // silenciosamente inaccesible. Fuera de producción (Development, etc.) se mantiene el
            // comportamiento de siempre: queda sin sembrar y listo, es un escenario válido de dev.
            if (_environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "Faltan una o más claves de configuración 'AdminBootstrap:Cedula', 'AdminBootstrap:Email' o " +
                    "'AdminBootstrap:Password'. Son obligatorias en producción para poder crear la cuenta de " +
                    "Administrador de arranque.");
            }

            return;
        }

        var adminRole = await _db.Roles.FirstAsync(r => r.Name == RoleNames.Administrador, cancellationToken);

        _db.Users.Add(new User
        {
            Cedula = adminCedula.Trim(),
            Email = adminEmail.Trim().ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(adminPassword),
            // Igual que cualquier usuario nuevo: debe cambiar la contraseña de arranque en su primer login.
            MustChangePassword = true,
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
