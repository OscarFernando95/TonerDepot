using Microsoft.EntityFrameworkCore;
using Npgsql;
using Toner.Application.Common;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance;
using Toner.Application.Maintenance.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Entities;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// AUDIT.md H-11 (Paso 3): los filtros del listado de cronogramas se traducen a SQL real (InMemory no lo prueba).
[Collection(nameof(RlsFixtureCollection))]
public class MaintenanceSchedulesFilterPostgresTests
{
    private static readonly Guid ContractA = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private readonly RlsFixture _fixture;

    public MaintenanceSchedulesFilterPostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    [PostgresFact]
    public async Task FiltrosSeTraducenASqlReal_YAcotanElListado()
    {
        await _fixture.EnsureSeededAsync();
        Guid scheduleId;
        await using (var owner = OwnerContext())
        {
            var schedule = TestEntities.MaintenanceSchedule(new Asset { Id = RlsFixture.AssetOfClientA }, new Contract { Id = ContractA });
            schedule.ClientId = RlsFixture.ClientA;
            owner.MaintenanceSchedules.Add(schedule);
            await owner.SaveChangesAsync();
            scheduleId = schedule.Id;
        }

        try
        {
            var tenant = new TenantContextAccessor();
            using var staff = tenant.Push(() => TenantContext.Staff);
            await using var db = new TonerDbContext(new DbContextOptionsBuilder<TonerDbContext>()
                .UseNpgsql(PostgresFactAttribute.ConnectionString).AddInterceptors(new TenantContextInterceptor(tenant)).Options);
            var service = new MaintenanceScheduleService(db, new MaintenanceScheduleEngine(db));

            var all = await service.ListAsync(1, 200, new MaintenanceScheduleListFilter(ContractId: ContractA, IsActive: true));
            Assert.Contains(all.Items, s => s.Id == scheduleId);

            var bySerial = await service.ListAsync(1, 200, new MaintenanceScheduleListFilter(Search: "SN-RLS-A", ClientId: RlsFixture.ClientA));
            Assert.Contains(bySerial.Items, s => s.Id == scheduleId);

            var none = await service.ListAsync(1, 200, new MaintenanceScheduleListFilter(Search: "no-existe-" + Guid.NewGuid().ToString("N")));
            Assert.Empty(none.Items);
            Assert.Equal(0, none.TotalCount);

            var paused = await service.ListAsync(1, 200, new MaintenanceScheduleListFilter(ContractId: ContractA, IsActive: false));
            Assert.DoesNotContain(paused.Items, s => s.Id == scheduleId);
        }
        finally
        {
            await using var cleanup = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
            await cleanup.OpenAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM \"MaintenanceSchedules\" WHERE \"Id\" = @s", cleanup);
            cmd.Parameters.AddWithValue("s", scheduleId);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
