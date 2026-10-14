using Microsoft.EntityFrameworkCore;
using Npgsql;
using Toner.Application.Assets;
using Toner.Application.Assets.Dtos;
using Toner.Application.Common;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// AUDIT.md H-11 (Paso 3) contra Postgres real y con el rol de la aplicación: activos con más de una página, orden
// estable sin repetidos, filtros traducidos a SQL real y aislamiento del Cliente (RLS + AND del servicio).
[Collection(nameof(RlsFixtureCollection))]
public class AssetsPagingPostgresTests
{
    private const int Total = 230;
    private static readonly Guid LocationA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private readonly RlsFixture _fixture;

    public AssetsPagingPostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    private static TonerDbContext AppContext(TenantContextAccessor tenant) =>
        new(new DbContextOptionsBuilder<TonerDbContext>()
            .UseNpgsql(PostgresFactAttribute.ConnectionString)
            .AddInterceptors(new TenantContextInterceptor(tenant))
            .Options);

    [PostgresFact]
    public async Task MasDeUnaPagina_SinRepetidos_FiltrosYAislamientoDelCliente()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PGH11A" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        try
        {
            await using (var owner = OwnerContext())
            {
                var modelId = await owner.AssetModels.Select(m => m.Id).FirstAsync();
                for (var i = 0; i < Total; i++)
                {
                    owner.Assets.Add(new Asset
                    {
                        AssetModelId = modelId, SerialNumber = $"{marker}-{i:D3}", Type = AssetType.Impresora,
                        LifecycleStatus = i % 10 == 0 ? AssetLifecycleStatus.EnMantenimiento : AssetLifecycleStatus.Instalado,
                        CurrentClientLocationId = LocationA
                    });
                }
                await owner.SaveChangesAsync();
            }

            var tenant = new TenantContextAccessor();
            using var staff = tenant.Push(() => TenantContext.Staff);
            await using var db = AppContext(tenant);
            var service = new AssetService(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));
            var coordinator = new RequestingUser(Guid.NewGuid(), RoleNames.Coordinador, null, null);
            var byMarker = new AssetListFilter(Search: marker);

            var first = await service.ListAsync(coordinator, 1, 200, byMarker);
            var second = await service.ListAsync(coordinator, 2, 200, byMarker);
            Assert.Equal(Total, first.TotalCount);
            Assert.Equal(200, first.Items.Count);
            Assert.Equal(Total - 200, second.Items.Count);
            Assert.Equal(Total, first.Items.Concat(second.Items).Select(a => a.Id).Distinct().Count());

            var inMaintenance = await service.ListAsync(coordinator, 1, 200, new AssetListFilter(Search: marker, Status: "EnMantenimiento"));
            Assert.Equal(Total / 10, inMaintenance.TotalCount);

            var atLocation = await service.ListAsync(coordinator, 1, 200, new AssetListFilter(Search: marker, ClientLocationId: LocationA, ClientId: RlsFixture.ClientA));
            Assert.Equal(Total, atLocation.TotalCount);

            // Cliente B, pasando el clientId y la sede del A: no ve nada (RLS + AND del servicio).
            var asClientB = new RequestingUser(Guid.NewGuid(), RoleNames.Cliente, RlsFixture.ClientB, null);
            using var clientScope = tenant.Push(() => TenantContext.ForClient(RlsFixture.ClientB));
            await using var clientDb = AppContext(tenant);
            var clientService = new AssetService(clientDb, new MaintenanceScheduleEngine(clientDb), TestAssignment.Create(clientDb));
            var leaked = await clientService.ListAsync(asClientB, 1, 200,
                new AssetListFilter(Search: marker, ClientId: RlsFixture.ClientA, ClientLocationId: LocationA));
            Assert.Empty(leaked.Items);
        }
        finally
        {
            await using var cleanup = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
            await cleanup.OpenAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM \"Assets\" WHERE \"SerialNumber\" LIKE @p", cleanup);
            cmd.Parameters.AddWithValue("p", marker + "-%");
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
