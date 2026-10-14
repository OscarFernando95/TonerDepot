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

// AUDIT.md H-11 (Paso 3) contra Postgres real: lecturas de contador con más de una página, orden estable sin
// repetidos, total del filtro y que un Técnico no amplíe su alcance con filtros.
[Collection(nameof(RlsFixtureCollection))]
public class MeterReadingsPagingPostgresTests
{
    private const int Total = 230;
    private static readonly Guid LocationA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private readonly RlsFixture _fixture;

    public MeterReadingsPagingPostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    private static TonerDbContext AppContext(TenantContextAccessor tenant) =>
        new(new DbContextOptionsBuilder<TonerDbContext>()
            .UseNpgsql(PostgresFactAttribute.ConnectionString)
            .AddInterceptors(new TenantContextInterceptor(tenant))
            .Options);

    [PostgresFact]
    public async Task MasDeUnaPagina_SinRepetidos_TotalDelFiltro_YElTecnicoNoAmpliaSuAlcance()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PGH11M" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
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
                        LifecycleStatus = AssetLifecycleStatus.Instalado, CurrentClientLocationId = LocationA
                    });
                }
                await owner.SaveChangesAsync();
            }

            var tenant = new TenantContextAccessor();
            using var staff = tenant.Push(() => TenantContext.Staff);
            await using var db = AppContext(tenant);
            var service = new AssetService(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));
            var coordinator = new RequestingUser(Guid.NewGuid(), RoleNames.Coordinador, null, null);
            var byMarker = new MeterReadingListFilter(Search: marker);

            var first = await service.ListForMeterReadingAsync(coordinator, 1, 200, byMarker);
            var second = await service.ListForMeterReadingAsync(coordinator, 2, 200, byMarker);

            Assert.Equal(Total, first.TotalCount);
            Assert.Equal(200, first.Items.Count);
            Assert.Equal(Total - 200, second.Items.Count);
            Assert.Equal(Total, first.Items.Concat(second.Items).Select(i => i.AssetId).Distinct().Count());

            var one = await service.ListForMeterReadingAsync(coordinator, 1, 200, new MeterReadingListFilter(Search: $"{marker}-007"));
            Assert.Equal(1, one.TotalCount);

            // Un técnico sin vínculos ni zona: ni el texto ni el clientId lo hacen ver más.
            var technician = new RequestingUser(Guid.NewGuid(), RoleNames.Tecnico, null, Guid.NewGuid());
            var asTech = await service.ListForMeterReadingAsync(technician, 1, 200, new MeterReadingListFilter(Search: marker, ClientId: RlsFixture.ClientA));
            Assert.Empty(asTech.Items);
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
