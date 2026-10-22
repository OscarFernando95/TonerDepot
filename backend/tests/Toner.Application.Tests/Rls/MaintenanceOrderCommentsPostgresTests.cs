using Microsoft.EntityFrameworkCore;
using Toner.Application.Common;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// MaintenanceOrderComments lleva su propia política RLS por empresa. Contra Postgres real y con el rol de la aplicación: el
// cliente de una empresa lee y escribe el hilo de SUS órdenes y nada de otra empresa; el staff ve todo.
[Collection(nameof(RlsFixtureCollection))]
public class MaintenanceOrderCommentsPostgresTests
{
    private static readonly Guid ContractId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    private readonly RlsFixture _fixture;

    public MaintenanceOrderCommentsPostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    private static TonerDbContext AppContext(TenantContextAccessor tenant) =>
        new(new DbContextOptionsBuilder<TonerDbContext>()
            .UseNpgsql(PostgresFactAttribute.ConnectionString)
            .AddInterceptors(new TenantContextInterceptor(tenant))
            .Options);

    [PostgresFact]
    public async Task ElClienteSoloVeYEscribeElHiloDeSuEmpresa_ElServicioLoAplicaSobreRls()
    {
        await _fixture.EnsureSeededAsync();
        Guid scheduleId = Guid.Empty;
        try
        {
            Guid orderA;
            await using (var owner = OwnerContext())
            {
                var schedule = TestEntities.MaintenanceSchedule(new Asset { Id = RlsFixture.AssetOfClientA }, new Contract { Id = ContractId });
                schedule.ClientId = RlsFixture.ClientA;
                scheduleId = schedule.Id;
                owner.MaintenanceSchedules.Add(schedule);
                await owner.SaveChangesAsync();
                var order = new MaintenanceOrder
                {
                    MaintenanceScheduleId = schedule.Id, ClientId = RlsFixture.ClientA, AssetId = RlsFixture.AssetOfClientA,
                    Status = MaintenanceOrderStatus.Asignada, ScheduledDate = DateTime.UtcNow, IncludesGeneral = true
                };
                owner.MaintenanceOrders.Add(order);
                await owner.SaveChangesAsync();
                orderA = order.Id;
            }

            var tenant = new TenantContextAccessor();
            var asClientA = new RequestingUser(RlsFixture.ClientUserA, RoleNames.Cliente, RlsFixture.ClientA, null);
            var asClientB = new RequestingUser(RlsFixture.ClientUserB, RoleNames.Cliente, RlsFixture.ClientB, null);

            // El cliente A comenta su orden: la fila queda con el ClientId de A y A la lee.
            using (tenant.Push(() => TenantContext.ForClient(RlsFixture.ClientA)))
            {
                await using var db = AppContext(tenant);
                var service = new MaintenanceOrderService(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));
                await service.AddCommentAsync(asClientA, orderA, "El jueves no podemos");
                var thread = await service.ListCommentsAsync(asClientA, orderA);
                Assert.Equal("El jueves no podemos", Assert.Single(thread).Body);
            }

            // El cliente B no ve la orden de A ni su hilo (RLS lo oculta; el servicio responde sin filtrar nada).
            using (tenant.Push(() => TenantContext.ForClient(RlsFixture.ClientB)))
            {
                await using var db = AppContext(tenant);
                Assert.Empty(await db.MaintenanceOrderComments.Where(c => c.MaintenanceOrderId == orderA).ToListAsync());
                var service = new MaintenanceOrderService(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));
                await Assert.ThrowsAnyAsync<Exception>(() => service.AddCommentAsync(asClientB, orderA, "intruso"));
            }

            // El staff ve el hilo.
            using (tenant.Push(() => TenantContext.Staff))
            {
                await using var db = AppContext(tenant);
                Assert.Equal(1, await db.MaintenanceOrderComments.CountAsync(c => c.MaintenanceOrderId == orderA));
            }
        }
        finally
        {
            await using var cleanup = OwnerContext();
            await cleanup.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"MaintenanceOrderComments\" WHERE \"MaintenanceOrderId\" IN (SELECT \"Id\" FROM \"MaintenanceOrders\" WHERE \"MaintenanceScheduleId\" = {0})", scheduleId);
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"MaintenanceOrders\" WHERE \"MaintenanceScheduleId\" = {0}", scheduleId);
            await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"MaintenanceSchedules\" WHERE \"Id\" = {0}", scheduleId);
        }
    }
}
