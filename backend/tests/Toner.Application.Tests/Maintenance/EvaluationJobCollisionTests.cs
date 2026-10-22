using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Maintenance;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Jobs;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Maintenance;

// MANT-1: el índice único de órdenes abiertas rechaza la del job cuando un técnico (o el staff) abrió una sobre el mismo
// equipo a mitad del lote. Antes eso dejaba dos órdenes abiertas; ahora el guardado del lote falla, y el job tiene que
// recuperarse solo en vez de perder el lote entero.
public class EvaluationJobCollisionTests
{
    // En el primer guardado: un "técnico" abre una orden sobre el cronograma ANTES de que el lote se guarde, y el
    // guardado del lote falla como lo haría el índice único (ConflictException).
    private sealed class OpenOrderThenConflictOnFirstSave : SaveChangesInterceptor
    {
        private readonly string _dbName;
        private readonly Guid _scheduleId;
        private readonly Guid _assetId;
        private readonly Guid _clientId;
        private bool _fired;

        public OpenOrderThenConflictOnFirstSave(string dbName, Guid scheduleId, Guid assetId, Guid clientId)
        {
            _dbName = dbName;
            _scheduleId = scheduleId;
            _assetId = assetId;
            _clientId = clientId;
        }

        public int SaveAttempts { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            SaveAttempts++;
            if (!_fired)
            {
                _fired = true;
                using var other = TonerTestDb.CreateContext(_dbName);
                other.Add(new MaintenanceOrder
                {
                    MaintenanceScheduleId = _scheduleId, AssetId = _assetId, ClientId = _clientId,
                    Status = MaintenanceOrderStatus.Asignada, ScheduledDate = DateTime.UtcNow, IncludesUnits = true, IsManual = true
                });
                await other.SaveChangesAsync(cancellationToken);
                throw new ConflictException("Este activo ya tiene una orden de mantenimiento abierta.");
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private static async Task<(string DbName, Guid ScheduleA, Guid AssetA, Guid ClientId, Guid ScheduleB)> SeedAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var location = TestEntities.ClientLocation(client, city);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        db.AddRange(city, client, contract, location, brand, model);

        MaintenanceSchedule Due(out Asset asset)
        {
            asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
            var schedule = TestEntities.MaintenanceSchedule(asset, contract, nextUnitsDueAt: DateTime.UtcNow.AddDays(-1));
            schedule.ClientId = client.Id;
            db.AddRange(asset, schedule);
            return schedule;
        }

        var a = Due(out var assetA);
        var b = Due(out _);
        await db.SaveChangesAsync();
        return (dbName, a.Id, assetA.Id, client.Id, b.Id);
    }

    [Fact]
    public async Task ChoqueConUnaOrdenAbiertaAMitadDelLote_ElJobSeRepiteUnaVez_YNoPierdeElLote()
    {
        var s = await SeedAsync();
        var interceptor = new OpenOrderThenConflictOnFirstSave(s.DbName, s.ScheduleA, s.AssetA, s.ClientId);
        using var db = TonerTestDb.CreateContext(s.DbName, interceptor);
        var job = new MaintenanceScheduleEvaluationJob(
            db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db), new FakeExceptionLogger(),
            NullLogger<MaintenanceScheduleEvaluationJob>.Instance, new TenantContextAccessor());

        var created = await job.RunAsync();

        // El equipo que chocó ya tiene la orden del técnico; el job solo generó la del otro (no se perdió).
        Assert.Equal(1, created);
        Assert.Equal(2, interceptor.SaveAttempts);

        using var check = TonerTestDb.CreateContext(s.DbName);
        var orders = await check.MaintenanceOrders.ToListAsync();
        Assert.Equal(2, orders.Count);
        Assert.Single(orders, o => o.MaintenanceScheduleId == s.ScheduleA);
        Assert.Single(orders, o => o.MaintenanceScheduleId == s.ScheduleB);
    }

    [Fact]
    public async Task UnErrorQueNoEsUnChoque_SigueSubiendoParaQueHangfireReintente()
    {
        var s = await SeedAsync();
        var spy = new SaveChangesSpyInterceptor { FailOnSaveNumber = 1 };
        using var db = TonerTestDb.CreateContext(s.DbName, spy);
        var logger = new FakeExceptionLogger();
        var job = new MaintenanceScheduleEvaluationJob(
            db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db), logger,
            NullLogger<MaintenanceScheduleEvaluationJob>.Instance, new TenantContextAccessor());

        await Assert.ThrowsAsync<SimulatedSaveFailureException>(() => job.RunAsync());

        Assert.Single(logger.LoggedExceptions);
        Assert.Equal(1, spy.SaveCount);
    }
}
