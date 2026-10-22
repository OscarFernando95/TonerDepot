using Microsoft.EntityFrameworkCore;
using Toner.Application.ClientPortal;
using Toner.Application.Common;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Common;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// El inicio del cliente contra Postgres real y con el rol de la aplicación: las consultas (orden por "abierto", subconsultas
// de conteo, recortes) se traducen a SQL de verdad y cada cliente ve solo lo de su empresa.
[Collection(nameof(RlsFixtureCollection))]
public class ClientPortalPostgresTests
{
    private readonly RlsFixture _fixture;

    public ClientPortalPostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static TonerDbContext AppContext(TenantContextAccessor tenant) =>
        new(new DbContextOptionsBuilder<TonerDbContext>()
            .UseNpgsql(PostgresFactAttribute.ConnectionString)
            .AddInterceptors(new TenantContextInterceptor(tenant))
            .Options);

    [PostgresFact]
    public async Task CadaClienteVeSoloSuResumen_ContraRls()
    {
        await _fixture.EnsureSeededAsync();
        var tenant = new TenantContextAccessor();

        using (tenant.Push(() => TenantContext.ForClient(RlsFixture.ClientA)))
        {
            await using var db = AppContext(tenant);
            var home = await new ClientPortalService(db).GetHomeAsync(
                new RequestingUser(RlsFixture.ClientUserA, RoleNames.Cliente, RlsFixture.ClientA, null));

            Assert.Equal("Cliente A RLS", home.ClientName);
            Assert.True(home.OpenTickets >= 1);
            Assert.True(home.InstalledAssets >= 1);
            Assert.All(home.RecentTickets, t => Assert.Equal("Sede A", t.LocationName));
            Assert.All(home.UpcomingMaintenance, m => Assert.NotEqual("Sede B", m.LocationName));
        }

        using (tenant.Push(() => TenantContext.ForClient(RlsFixture.ClientB)))
        {
            await using var db = AppContext(tenant);
            var home = await new ClientPortalService(db).GetHomeAsync(
                new RequestingUser(RlsFixture.ClientUserB, RoleNames.Cliente, RlsFixture.ClientB, null));

            Assert.Equal("Cliente B RLS", home.ClientName);
            Assert.DoesNotContain(home.RecentTickets, t => t.LocationName == "Sede A");
            Assert.DoesNotContain(home.Installations, i => i.LocationName == "Sede A");
        }
    }
}
