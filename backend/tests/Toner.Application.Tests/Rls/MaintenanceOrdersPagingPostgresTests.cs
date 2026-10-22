using Microsoft.EntityFrameworkCore;
using Npgsql;
using Toner.Application.Common;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance;
using Toner.Application.Maintenance.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// AUDIT.md H-11 (Paso 3) contra Postgres real: órdenes de mantenimiento con más de una página, orden estable sin
// repetidos, total del filtro y alcance del Técnico intacto.
[Collection(nameof(RlsFixtureCollection))]
public class MaintenanceOrdersPagingPostgresTests
{
    private const int Total = 230;
    private static readonly Guid ContractA = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    private readonly RlsFixture _fixture;

    public MaintenanceOrdersPagingPostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    private static TonerDbContext AppContext(TenantContextAccessor tenant) =>
        new(new DbContextOptionsBuilder<TonerDbContext>()
            .UseNpgsql(PostgresFactAttribute.ConnectionString)
            .AddInterceptors(new TenantContextInterceptor(tenant))
            .Options);

    [PostgresFact]
    public async Task MasDeUnaPagina_SinRepetidos_FiltroPorEstado_YAlcanceDelTecnico()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-H11-O-" + Guid.NewGuid().ToString("N")[..6];
        Guid scheduleId = Guid.Empty;
        try
        {
            await using (var owner = OwnerContext())
            {
                var schedule = TestEntities.MaintenanceSchedule(new Asset { Id = RlsFixture.AssetOfClientA }, new Contract { Id = ContractA });
                schedule.ClientId = RlsFixture.ClientA;
                scheduleId = schedule.Id;
                owner.MaintenanceSchedules.Add(schedule);
                var sameInstant = DateTime.UtcNow.AddMinutes(-5); // CreatedAt igual: el orden depende del desempate
                for (var i = 0; i < Total; i++)
                {
                    owner.MaintenanceOrders.Add(new MaintenanceOrder
                    {
                        MaintenanceScheduleId = schedule.Id, ClientId = RlsFixture.ClientA, AssetId = RlsFixture.AssetOfClientA,
                        // Solo una puede estar abierta por cronograma (índice único parcial, MANT-1): el resto, ya cerradas.
                        Status = i % 5 == 0 ? MaintenanceOrderStatus.Completada
                            : i == 1 ? MaintenanceOrderStatus.Pendiente
                            : MaintenanceOrderStatus.Cancelada,
                        ScheduledDate = sameInstant, CreatedAt = sameInstant
                    });
                }
                await owner.SaveChangesAsync();
            }

            var tenant = new TenantContextAccessor();
            using var staff = tenant.Push(() => TenantContext.Staff);
            await using var db = AppContext(tenant);
            var service = new MaintenanceOrderService(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));
            var coordinator = new RequestingUser(Guid.NewGuid(), RoleNames.Coordinador, null, null);
            var onlyMine = new MaintenanceOrderListFilter(ClientId: RlsFixture.ClientA);

            // Cuenta las órdenes sembradas por esta prueba usando el estado, que sí distingue 46 vs 184.
            var completed = await service.ListAsync(coordinator, 1, 200, new MaintenanceOrderListFilter(Status: "Completada", ClientId: RlsFixture.ClientA));
            var cancelled = await service.ListAsync(coordinator, 1, 200, new MaintenanceOrderListFilter(Status: "Cancelada", ClientId: RlsFixture.ClientA));
            var pending = await service.ListAsync(coordinator, 1, 200, new MaintenanceOrderListFilter(Status: "Pendiente", ClientId: RlsFixture.ClientA));
            Assert.True(completed.Items.All(o => o.Status == "Completada"));
            Assert.True(pending.Items.All(o => o.Status == "Pendiente"));
            Assert.True(pending.TotalCount >= 1);
            Assert.True(cancelled.TotalCount >= Total - Total / 5 - 1);
            Assert.True(completed.TotalCount >= Total / 5);

            var first = await service.ListAsync(coordinator, 1, 200, onlyMine);
            var second = await service.ListAsync(coordinator, 2, 200, onlyMine);
            Assert.True(first.TotalCount >= Total);
            Assert.Equal(200, first.Items.Count);
            Assert.Equal(first.Items.Count + second.Items.Count, first.Items.Concat(second.Items).Select(o => o.Id).Distinct().Count());

            var technician = new RequestingUser(Guid.NewGuid(), RoleNames.Tecnico, null, Guid.NewGuid());
            var asTech = await service.ListAsync(technician, 1, 200, new MaintenanceOrderListFilter(ClientId: RlsFixture.ClientA));
            Assert.Empty(asTech.Items);
        }
        finally
        {
            await using var cleanup = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
            await cleanup.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "DELETE FROM \"MaintenanceOrders\" WHERE \"MaintenanceScheduleId\" = @s; DELETE FROM \"MaintenanceSchedules\" WHERE \"Id\" = @s", cleanup);
            cmd.Parameters.AddWithValue("s", scheduleId);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
