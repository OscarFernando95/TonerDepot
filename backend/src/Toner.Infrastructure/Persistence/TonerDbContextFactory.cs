using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Toner.Infrastructure.Persistence;

// Factory de tiempo de diseño para las herramientas de EF Core (dotnet ef migrations/database
// update). Existe por dos razones, ambas consecuencia del hallazgo #5 (RLS):
//
// 1. Las migraciones deben correr con el rol OWNER (MigrationsConnection), no con el rol de la
//    aplicación (toner_app), que deliberadamente no puede hacer DDL.
// 2. El contexto que construye la app en runtime lleva TenantContextInterceptor, que lanza si no
//    hay TenantContext establecido. Las herramientas de EF no corren dentro de una request ni de un
//    scope de background, así que sin esta factory `dotnet ef database update` fallaría siempre.
//    Aquí se construye un DbContext sin ese interceptor a propósito.
//
// ⚠️ Las tablas con FORCE ROW LEVEL SECURITY (ClientLocations, Contracts, ServiceTickets) aplican
// sus políticas incluso al owner. Cualquier migración futura con UPDATE/DELETE sobre esas tablas
// debe empezar con `SET LOCAL app.is_staff = 'on';` dentro del mismo migrationBuilder.Sql(), o
// afectará 0 filas EN SILENCIO. Ver README, sección "RLS y migraciones".
public class TonerDbContextFactory : IDesignTimeDbContextFactory<TonerDbContext>
{
    public TonerDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("MigrationsConnection")
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexión 'MigrationsConnection'. Las migraciones deben correr con el " +
                "rol owner de Postgres, no con el rol de la aplicación (que no tiene privilegios de DDL).");

        var options = new DbContextOptionsBuilder<TonerDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly("Toner.Infrastructure"))
            .Options;

        return new TonerDbContext(options);
    }
}
