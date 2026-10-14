using Microsoft.EntityFrameworkCore;
using Npgsql;
using Toner.Application.Common;
using Toner.Application.Common.Interfaces;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Tickets;
using Toner.Application.Tickets.Dtos;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// AUDIT.md H-11 (Paso 3) contra Postgres real y con el rol de la aplicación: más de una página, orden estable sin
// repetidos, total del filtro, y el aislamiento por RLS intacto aunque se pase el clientId de otro.
[Collection(nameof(RlsFixtureCollection))]
public class TicketsPagingPostgresTests
{
    private static readonly Guid LocationA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private const int Total = 230;

    private readonly RlsFixture _fixture;

    public TicketsPagingPostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    private static TonerDbContext AppContext(TenantContextAccessor tenant) =>
        new(new DbContextOptionsBuilder<TonerDbContext>()
            .UseNpgsql(PostgresFactAttribute.ConnectionString)
            .AddInterceptors(new TenantContextInterceptor(tenant))
            .Options);

    [PostgresFact]
    public async Task MasDeUnaPagina_SinRepetidos_TotalDelFiltro_YAislamientoIntacto()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-H11-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            Guid reporter;
            await using (var owner = OwnerContext())
            {
                reporter = await owner.Users.Select(u => u.Id).FirstAsync();
                var sameInstant = DateTime.UtcNow.AddMinutes(-5); // CreatedAt IGUAL en todas: el orden depende del desempate
                for (var i = 0; i < Total; i++)
                {
                    owner.ServiceTickets.Add(new ServiceTicket
                    {
                        ClientLocationId = LocationA, ClientId = RlsFixture.ClientA, ReportedByUserId = reporter,
                        Description = $"{marker} #{i}", Status = i % 10 == 0 ? ServiceTicketStatus.Cerrado : ServiceTicketStatus.Abierto,
                        Priority = ServiceTicketPriority.Media, CreatedAt = sameInstant
                    });
                }
                await owner.SaveChangesAsync();
            }

            var tenant = new TenantContextAccessor();
            using var staff = tenant.Push(() => TenantContext.Staff);
            await using var db = AppContext(tenant);
            var service = new ServiceTicketService(db, TestAssignment.Create(db));
            var coordinator = new RequestingUser(Guid.NewGuid(), RoleNames.Coordinador, null, null);
            var byMarker = new ServiceTicketListFilter(Search: marker);

            var first = await service.ListAsync(coordinator, 1, 200, byMarker);
            var second = await service.ListAsync(coordinator, 2, 200, byMarker);

            Assert.Equal(Total, first.TotalCount);
            Assert.Equal(200, first.Items.Count);
            Assert.Equal(Total - 200, second.Items.Count);
            Assert.True(first.HasMore);
            var ids = first.Items.Concat(second.Items).Select(t => t.Id).ToList();
            Assert.Equal(Total, ids.Distinct().Count()); // ninguna fila repetida ni saltada entre páginas

            var closed = await service.ListAsync(coordinator, 1, 200, new ServiceTicketListFilter(Status: "Cerrado", Search: marker));
            Assert.Equal(Total / 10, closed.TotalCount);

            // Aislamiento: un Cliente B pasando el clientId del A sigue sin ver nada (RLS + AND del servicio).
            var asClientB = new RequestingUser(Guid.NewGuid(), RoleNames.Cliente, RlsFixture.ClientB, null);
            using var clientScope = tenant.Push(() => TenantContext.ForClient(RlsFixture.ClientB));
            await using var clientDb = AppContext(tenant);
            var clientService = new ServiceTicketService(clientDb, TestAssignment.Create(clientDb));
            var leaked = await clientService.ListAsync(asClientB, 1, 200, new ServiceTicketListFilter(ClientId: RlsFixture.ClientA, Search: marker));
            Assert.Empty(leaked.Items);
        }
        finally
        {
            await using var cleanup = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
            await cleanup.OpenAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM \"ServiceTickets\" WHERE \"Description\" LIKE @p", cleanup);
            cmd.Parameters.AddWithValue("p", marker + "%");
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
