using Microsoft.EntityFrameworkCore;
using Toner.Application.Common;
using Toner.Application.Common.Interfaces;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// UserClientLocations (sedes permitidas por usuario Cliente) es una tabla alcanzable desde Clients: lleva su propia política
// RLS por empresa, con el rol de la aplicación. Aquí se comprueba contra Postgres real que un cliente solo ve y escribe filas
// de SU empresa, y que el staff las ve todas.
[Collection(nameof(RlsFixtureCollection))]
public class UserClientLocationsPostgresTests
{
    private static readonly Guid LocationA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid LocationB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private readonly RlsFixture _fixture;

    public UserClientLocationsPostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    private static TonerDbContext AppContext(TenantContextAccessor tenant) =>
        new(new DbContextOptionsBuilder<TonerDbContext>()
            .UseNpgsql(PostgresFactAttribute.ConnectionString)
            .AddInterceptors(new TenantContextInterceptor(tenant))
            .Options);

    private static async Task CleanupAsync()
    {
        await using var cleanup = OwnerContext();
        await cleanup.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"UserClientLocations\" WHERE \"UserId\" = ANY({0})",
            new[] { RlsFixture.ClientUserA, RlsFixture.ClientUserB });
    }

    [PostgresFact]
    public async Task CadaClienteVeSoloLasFilasDeSuEmpresa_ElStaffVeTodas()
    {
        await _fixture.EnsureSeededAsync();
        await CleanupAsync();
        try
        {
            await using (var owner = OwnerContext())
            {
                owner.UserClientLocations.AddRange(
                    new UserClientLocation { UserId = RlsFixture.ClientUserA, ClientLocationId = LocationA, ClientId = RlsFixture.ClientA },
                    new UserClientLocation { UserId = RlsFixture.ClientUserB, ClientLocationId = LocationB, ClientId = RlsFixture.ClientB });
                await owner.SaveChangesAsync();
            }

            var tenant = new TenantContextAccessor();

            using (tenant.Push(() => TenantContext.ForClient(RlsFixture.ClientA)))
            {
                await using var db = AppContext(tenant);
                var rows = await db.UserClientLocations.ToListAsync();
                Assert.Equal(new[] { LocationA }, rows.Select(r => r.ClientLocationId));
            }

            using (tenant.Push(() => TenantContext.ForClient(RlsFixture.ClientB)))
            {
                await using var db = AppContext(tenant);
                Assert.Equal(new[] { LocationB }, (await db.UserClientLocations.ToListAsync()).Select(r => r.ClientLocationId));
            }

            using (tenant.Push(() => TenantContext.Staff))
            {
                await using var db = AppContext(tenant);
                var mine = await db.UserClientLocations
                    .Where(r => r.UserId == RlsFixture.ClientUserA || r.UserId == RlsFixture.ClientUserB).CountAsync();
                Assert.Equal(2, mine);
            }
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [PostgresFact]
    public async Task UnClienteNoPuedeEscribirUnaFilaDeOtraEmpresa()
    {
        await _fixture.EnsureSeededAsync();
        await CleanupAsync();
        try
        {
            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.ForClient(RlsFixture.ClientA));
            await using var db = AppContext(tenant);
            db.UserClientLocations.Add(new UserClientLocation
            {
                UserId = RlsFixture.ClientUserB, ClientLocationId = LocationB, ClientId = RlsFixture.ClientB
            });

            await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        }
        finally
        {
            await CleanupAsync();
        }
    }
}
