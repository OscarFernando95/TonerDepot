using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Toner.Application.Common;
using Toner.Application.Common.Interfaces;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Infrastructure.Persistence.Seed;

namespace Toner.Application.Tests.Seed;

// Verifica el hallazgo #4 de SECURITY_AUDIT.md: el admin de arranque ya no trae una cédula
// hardcodeada en el código, queda con MustChangePassword = true, y si falta configuración en
// producción el arranque debe fallar en vez de saltarse el seed en silencio.
public class DataSeederTests
{
    private static IConfiguration BuildConfiguration(IDictionary<string, string?>? overrides = null)
    {
        var defaults = new Dictionary<string, string?>
        {
            ["AdminBootstrap:Cedula"] = "9999999999",
            ["AdminBootstrap:Email"] = "bootstrap-admin@example.test",
            ["AdminBootstrap:Password"] = "S3ed-Only-Passw0rd!",
            ["AdminBootstrap:FullName"] = "Administrador Toner"
        };

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                defaults[key] = value;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(defaults).Build();
    }

    private static DataSeeder BuildSeeder(
        Infrastructure.Persistence.TonerDbContext db,
        IConfiguration configuration,
        string environmentName) =>
        new(db, configuration, new FakePasswordHasher(), new FakeHostEnvironment(environmentName), new TenantContextAccessor());

    [Fact]
    public async Task SeedAsync_ConConfiguracionCompleta_UsaLaCedulaDeConfiguracionYFuerzaCambioDeContrasena()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var seeder = BuildSeeder(db, BuildConfiguration(), Environments.Development);

        await seeder.SeedAsync();

        var admin = await db.Users.Include(u => u.Role)
            .SingleAsync(u => u.Role.Name == RoleNames.Administrador);

        Assert.Equal("9999999999", admin.Cedula);
        Assert.True(admin.MustChangePassword);
    }

    [Fact]
    public async Task SeedAsync_EnProduccionSinConfiguracion_FallaElArranque()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var incompleteConfig = BuildConfiguration(new Dictionary<string, string?> { ["AdminBootstrap:Cedula"] = null });
        var seeder = BuildSeeder(db, incompleteConfig, Environments.Production);

        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync());
    }

    [Fact]
    public async Task SeedAsync_FueraDeProduccionSinConfiguracion_NoCreaAdminYNoFalla()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var incompleteConfig = BuildConfiguration(new Dictionary<string, string?> { ["AdminBootstrap:Password"] = null });
        var seeder = BuildSeeder(db, incompleteConfig, Environments.Development);

        await seeder.SeedAsync();

        var hasAdmin = await db.Users.Include(u => u.Role).AnyAsync(u => u.Role.Name == RoleNames.Administrador);
        Assert.False(hasAdmin);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";
        public bool Verify(string password, string passwordHash) => passwordHash == $"hashed:{password}";
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Toner.Application.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
