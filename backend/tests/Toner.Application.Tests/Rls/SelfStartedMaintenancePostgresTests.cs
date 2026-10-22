using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Toner.Application.Common;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance;
using Toner.Application.Maintenance.Dtos;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// Un técnico que inicia su propio mantenimiento escribe una MaintenanceOrder (con ClientId denormalizado, FORCE RLS) y
// un AssignmentHistory con el rol de la aplicación. Con InMemory eso no se ejercita: aquí corre el flujo entero (iniciar,
// check-in, check-out) contra el Postgres real, con el contexto de un técnico (que la aplicación trata como staff).
[Collection(nameof(RlsFixtureCollection))]
public class SelfStartedMaintenancePostgresTests
{
    private readonly RlsFixture _fixture;

    public SelfStartedMaintenancePostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    private static TonerDbContext AppContext(TenantContextAccessor tenant) =>
        new(new DbContextOptionsBuilder<TonerDbContext>()
            .UseNpgsql(PostgresFactAttribute.ConnectionString)
            .AddInterceptors(new TenantContextInterceptor(tenant))
            .Options);

    private sealed record Seed(
        Guid ZoneId, Guid CityId, Guid ClientId, Guid ContractId, Guid LocationId, Guid BrandId, Guid ModelId,
        Guid AssetId, Guid ScheduleId, Guid UserId, Guid TechnicianId);

    private static async Task<Seed> SeedAsync(string marker)
    {
        await using var owner = OwnerContext();
        var role = await owner.Roles.FirstAsync(r => r.Name == RoleNames.Tecnico);
        var city = TestEntities.City(marker);
        var client = TestEntities.Client(marker);
        var contract = TestEntities.Contract(client);
        var location = TestEntities.ClientLocation(client, city);
        var brand = TestEntities.AssetBrand(marker);
        var model = TestEntities.AssetModel(brand, marker);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        var user = TestEntities.User(role);
        var technician = TestEntities.Technician(user, isActive: true, status: TechnicianStatus.Disponible);
        var coverage = TestEntities.Coverage(technician, city);
        var schedule = TestEntities.MaintenanceSchedule(
            asset, contract, nextGeneralDueCounter: 30_000, nextUnitsDueCounter: 30_000, nextConsumablesDueCounter: 60_000,
            lastGeneralMaintenanceAt: DateTime.UtcNow.AddMonths(-3), lastGeneralMaintenanceCounter: 0,
            lastUnitsMaintenanceAt: DateTime.UtcNow.AddMonths(-3), lastUnitsMaintenanceCounter: 0,
            lastConsumablesChangeAt: DateTime.UtcNow.AddMonths(-3), lastConsumablesChangeCounter: 0);
        schedule.ClientId = client.Id;

        owner.AddRange(coverage.Zone, city, client, contract, location, brand, model, asset, user, technician, coverage, schedule);
        await owner.SaveChangesAsync();
        return new Seed(
            coverage.ZoneId, city.Id, client.Id, contract.Id, location.Id, brand.Id, model.Id, asset.Id, schedule.Id, user.Id, technician.Id);
    }

    private static async Task CleanupAsync(Seed? s)
    {
        if (s is null) return;
        await using var owner = OwnerContext();
        async Task Sql(string sql, Guid id) => await owner.Database.ExecuteSqlRawAsync(sql, id);
        await Sql("DELETE FROM \"MeterReadings\" WHERE \"AssetId\" = {0}", s.AssetId);
        await Sql("DELETE FROM \"TimeLogs\" WHERE \"TechnicianId\" = {0}", s.TechnicianId);
        await Sql("DELETE FROM \"TechnicianAvailabilities\" WHERE \"TechnicianId\" = {0}", s.TechnicianId);
        await Sql("DELETE FROM \"AssignmentHistories\" WHERE \"TechnicianId\" = {0}", s.TechnicianId);
        await Sql("DELETE FROM \"MaintenanceOrders\" WHERE \"AssetId\" = {0}", s.AssetId);
        await Sql("DELETE FROM \"MaintenanceSchedules\" WHERE \"AssetId\" = {0}", s.AssetId);
        await Sql("DELETE FROM \"TechnicianAssets\" WHERE \"AssetId\" = {0}", s.AssetId);
        await Sql("DELETE FROM \"Assets\" WHERE \"Id\" = {0}", s.AssetId);
        await Sql("DELETE FROM \"TechnicianZones\" WHERE \"TechnicianId\" = {0}", s.TechnicianId);
        await Sql("DELETE FROM \"Technicians\" WHERE \"Id\" = {0}", s.TechnicianId);
        await Sql("DELETE FROM \"Users\" WHERE \"Id\" = {0}", s.UserId);
        await Sql("DELETE FROM \"ClientLocations\" WHERE \"Id\" = {0}", s.LocationId);
        await Sql("DELETE FROM \"Contracts\" WHERE \"Id\" = {0}", s.ContractId);
        await Sql("DELETE FROM \"Clients\" WHERE \"Id\" = {0}", s.ClientId);
        await Sql("DELETE FROM \"Cities\" WHERE \"Id\" = {0}", s.CityId);
        await Sql("DELETE FROM \"Zones\" WHERE \"Id\" = {0}", s.ZoneId);
        await Sql("DELETE FROM \"AssetModels\" WHERE \"Id\" = {0}", s.ModelId);
        await Sql("DELETE FROM \"AssetBrands\" WHERE \"Id\" = {0}", s.BrandId);
    }

    [PostgresFact]
    public async Task IniciarCheckInYCheckOut_ConElRolDeLaApp_ReiniciaSoloLasUnidades()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-SELF-" + Guid.NewGuid().ToString("N")[..8];
        Seed? seed = null;
        try
        {
            seed = await SeedAsync(marker);
            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);

            MaintenanceOrderDto order;
            await using (var db = AppContext(tenant))
            {
                order = await TestCheckIn.Create(db).StartMaintenanceAsync(seed.TechnicianId, new StartSelfMaintenanceRequest
                {
                    AssetId = seed.AssetId, IncludesUnits = true, Reason = marker
                });
            }

            Assert.Equal(nameof(MaintenanceOrderStatus.Asignada), order.Status);
            Assert.Equal(seed.TechnicianId, order.TechnicianId);

            await using (var db = AppContext(tenant))
            {
                await TestCheckIn.Create(db).CheckInAsync(seed.TechnicianId, new CheckInRequest { MaintenanceOrderId = order.Id });
            }

            await using (var db = AppContext(tenant))
            {
                await TestCheckIn.Create(db).CheckOutAsync(seed.TechnicianId, new CheckOutRequest { Resolved = true, InitialCounterValue = 12_000 });
            }

            await using var check = OwnerContext();
            var saved = await check.MaintenanceOrders.SingleAsync(o => o.Id == order.Id);
            Assert.Equal(MaintenanceOrderStatus.Completada, saved.Status);
            Assert.Equal(seed.ClientId, saved.ClientId);

            var schedule = await check.MaintenanceSchedules.SingleAsync(x => x.Id == seed.ScheduleId);
            Assert.Equal(12_000, schedule.LastUnitsMaintenanceCounter);
            Assert.Equal(12_000 + 30_000, schedule.NextUnitsDueCounter);
            Assert.Equal(0, schedule.LastGeneralMaintenanceCounter);
            Assert.Equal(30_000, schedule.NextGeneralDueCounter);
            Assert.Equal(0, schedule.LastConsumablesChangeCounter);
            Assert.Equal(60_000, schedule.NextConsumablesDueCounter);
            Assert.Equal(TechnicianStatus.Disponible, (await check.Technicians.SingleAsync(t => t.Id == seed.TechnicianId)).Status);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // El job de evaluación del cronograma y el técnico pueden coincidir sobre el mismo equipo. Aquí el job ya generó su
    // orden: el técnico NO debe poder abrir otra encima, y no debe quedar ninguna fila a medias.
    [PostgresFact]
    public async Task ConLaOrdenDelCronogramaYaAbierta_ElTecnicoNoCreaOtra()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-SELF-" + Guid.NewGuid().ToString("N")[..8];
        Seed? seed = null;
        try
        {
            seed = await SeedAsync(marker);
            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);

            await using (var jobDb = AppContext(tenant))
            {
                var generated = await new MaintenanceScheduleEngine(jobDb).EvaluateAsync(seed.AssetId, 999_999, DateTime.UtcNow);
                Assert.NotNull(generated);
                await jobDb.SaveChangesAsync();
            }

            await using (var db = AppContext(tenant))
            {
                await Assert.ThrowsAsync<Toner.Application.Common.Exceptions.ConflictException>(() =>
                    TestCheckIn.Create(db).StartMaintenanceAsync(seed.TechnicianId, new StartSelfMaintenanceRequest
                    {
                        AssetId = seed.AssetId, IncludesUnits = true
                    }));
            }

            await using var check = OwnerContext();
            Assert.Equal(1, await check.MaintenanceOrders.CountAsync(o => o.AssetId == seed.AssetId));
            Assert.Equal(0, await check.AssignmentHistories.CountAsync(h => h.TechnicianId == seed.TechnicianId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    // MANT-1: dos peticiones pasan el chequeo "¿hay una orden abierta?" antes de que cualquiera inserte. Pausa la del
    // técnico justo DESPUÉS de leer las órdenes (ya vio "ninguna") y deja que otra abra la suya completa: sin el índice
    // único parcial quedarían dos órdenes abiertas para el mismo cronograma.
    private sealed class PauseAfterOrdersRead : DbCommandInterceptor
    {
        private readonly Func<Task> _runOther;
        private bool _fired;

        public PauseAfterOrdersRead(Func<Task> runOther) => _runOther = runOther;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!_fired && command.CommandText.Contains("FROM \"MaintenanceOrders\""))
            {
                _fired = true;
                await _runOther();
            }

            return result;
        }
    }

    private static MaintenanceOrder OpenOrder(Seed s) => new()
    {
        MaintenanceScheduleId = s.ScheduleId, AssetId = s.AssetId, ClientId = s.ClientId,
        Status = MaintenanceOrderStatus.Pendiente, ScheduledDate = DateTime.UtcNow, IncludesUnits = true
    };

    [PostgresFact]
    public async Task ElIndiceUnico_RechazaUnaSegundaOrdenAbierta_YPermiteOtraTrasCompletarLaPrimera()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-SELF-" + Guid.NewGuid().ToString("N")[..8];
        Seed? seed = null;
        try
        {
            seed = await SeedAsync(marker);
            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);

            Guid firstId;
            await using (var db = AppContext(tenant))
            {
                var first = OpenOrder(seed);
                db.Add(first);
                await db.SaveChangesAsync();
                firstId = first.Id;
            }

            await using (var db = AppContext(tenant))
            {
                db.Add(OpenOrder(seed));
                var ex = await Assert.ThrowsAsync<Toner.Application.Common.Exceptions.ConflictException>(() => db.SaveChangesAsync());
                Assert.Contains("orden de mantenimiento abierta", ex.Message);
            }

            // Cerrada la primera, el cronograma vuelve a admitir una abierta.
            await using (var db = AppContext(tenant))
            {
                (await db.MaintenanceOrders.SingleAsync(o => o.Id == firstId)).Status = MaintenanceOrderStatus.Completada;
                await db.SaveChangesAsync();
            }

            await using (var db = AppContext(tenant))
            {
                db.Add(OpenOrder(seed));
                await db.SaveChangesAsync();
            }

            await using var check = OwnerContext();
            Assert.Equal(2, await check.MaintenanceOrders.CountAsync(o => o.AssetId == seed.AssetId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [PostgresFact]
    public async Task CarreraEntreElTecnicoYOtraOrdenAbierta_ElTecnicoRecibeUn409_YNoQuedanDosAbiertas()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-SELF-" + Guid.NewGuid().ToString("N")[..8];
        Seed? seed = null;
        try
        {
            seed = await SeedAsync(marker);
            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);

            // La otra petición (p. ej. el job del cronograma) guarda su orden abierta mientras el técnico está "pausado".
            async Task RunOther()
            {
                await using var other = OwnerContext();
                other.Add(OpenOrder(seed));
                await other.SaveChangesAsync();
            }

            var options = new DbContextOptionsBuilder<TonerDbContext>()
                .UseNpgsql(PostgresFactAttribute.ConnectionString)
                .AddInterceptors(new TenantContextInterceptor(tenant), new PauseAfterOrdersRead(RunOther))
                .Options;
            await using var technicianDb = new TonerDbContext(options);

            var ex = await Assert.ThrowsAsync<Toner.Application.Common.Exceptions.ConflictException>(() =>
                TestCheckIn.Create(technicianDb).StartMaintenanceAsync(
                    seed.TechnicianId, new StartSelfMaintenanceRequest { AssetId = seed.AssetId, IncludesUnits = true }));

            Assert.Contains("orden de mantenimiento abierta", ex.Message);

            await using var check = OwnerContext();
            Assert.Equal(1, await check.MaintenanceOrders.CountAsync(o => o.AssetId == seed.AssetId));
            Assert.Equal(0, await check.AssignmentHistories.CountAsync(h => h.TechnicianId == seed.TechnicianId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }
}
