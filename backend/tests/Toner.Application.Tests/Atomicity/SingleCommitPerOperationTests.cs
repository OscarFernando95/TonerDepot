using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Toner.Application.Assets;
using Toner.Application.Assets.Dtos;
using Toner.Application.Assignment;
using Toner.Application.Common;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance;
using Toner.Application.Technicians;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Tickets;
using Toner.Application.Tickets.Dtos;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Jobs;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Atomicity;

// CODE_QUALITY_AUDIT.md hallazgo #8. Cada operación de negocio debe terminar con EXACTAMENTE un
// SaveChangesAsync, para que un fallo a mitad no pueda dejar la mitad de la operación persistida.
//
// Cómo se prueba: con un interceptor que hace fallar el SEGUNDO SaveChanges. Si el código es
// correcto ese segundo nunca ocurre, la operación termina completa y el estado queda consistente.
// Si alguien reintroduce un commit intermedio, el fallo simulado se dispara y el test muestra el
// estado a medias exacto que el hallazgo describía. Hacer fallar el PRIMERO no distinguiría nada:
// el código correcto y el roto escribirían cero filas por igual.
//
// Cada test afirma además SaveCount == 1, que es la propiedad de forma directa.
public class SingleCommitPerOperationTests
{
    private static TechnicianCheckInService BuildCheckInService(TonerDbContext db) => TestCheckIn.Create(db);

    // ── Caso 1 (severidad 🔴): TechnicianCheckInService.CheckOutAsync ────────────────────────────
    // Antes eran hasta 3 commits. Si el último fallaba, la visita quedaba CERRADA (TimeLog con
    // EndTime, técnico Disponible, lectura grabada) pero el ticket seguía EnProceso — y el reintento
    // natural del técnico grababa una MeterReading DUPLICADA, corrompiendo los umbrales de
    // mantenimiento y el promedio de impresiones por mes del contrato.
    [Fact]
    public async Task CheckOutAsync_TicketResuelto_EsUnSoloCommit()
    {
        var dbName = Guid.NewGuid().ToString();
        var (assetId, technicianId, ticketId) = await SeedTicketVisitAsync(dbName);

        var spy = new SaveChangesSpyInterceptor { FailOnSaveNumber = 2 };
        using var actDb = TonerTestDb.CreateContext(dbName, spy);
        var service = BuildCheckInService(actDb);

        // Con contador: dispara la rama que graba MeterReading y puede generar orden — el camino con
        // más puntos de persistencia posibles.
        await service.CheckOutAsync(technicianId, new CheckOutRequest { Resolved = true, InitialCounterValue = 50_000 });

        Assert.Equal(1, spy.SaveCount);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var ticket = await assertDb.ServiceTickets.SingleAsync(t => t.Id == ticketId);
        var timeLog = await assertDb.TimeLogs.SingleAsync(l => l.ServiceTicketId == ticketId);
        var readings = await assertDb.MeterReadings.Where(m => m.AssetId == assetId).ToListAsync();
        var technician = await assertDb.Technicians.SingleAsync(t => t.Id == technicianId);

        // Todo o nada: la visita cerrada, el ticket resuelto y UNA sola lectura.
        Assert.Equal(ServiceTicketStatus.Resuelto, ticket.Status);
        Assert.NotNull(ticket.ResolvedAt);
        Assert.NotNull(timeLog.EndTime);
        Assert.Equal(TechnicianStatus.Disponible, technician.Status);
        Assert.Single(readings);
    }

    // Complemento del anterior: si el ÚNICO commit falla, no debe quedar absolutamente nada — ni la
    // visita cerrada, ni el técnico liberado, ni la lectura.
    [Fact]
    public async Task CheckOutAsync_SiFallaElUnicoCommit_NoPersisteNada()
    {
        var dbName = Guid.NewGuid().ToString();
        var (assetId, technicianId, ticketId) = await SeedTicketVisitAsync(dbName);

        var spy = new SaveChangesSpyInterceptor { FailOnSaveNumber = 1 };
        using var actDb = TonerTestDb.CreateContext(dbName, spy);
        var service = BuildCheckInService(actDb);

        await Assert.ThrowsAsync<SimulatedSaveFailureException>(() =>
            service.CheckOutAsync(technicianId, new CheckOutRequest { Resolved = true, InitialCounterValue = 50_000 }));

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var ticket = await assertDb.ServiceTickets.SingleAsync(t => t.Id == ticketId);
        var timeLog = await assertDb.TimeLogs.SingleAsync(l => l.ServiceTicketId == ticketId);
        var technician = await assertDb.Technicians.SingleAsync(t => t.Id == technicianId);

        Assert.Equal(ServiceTicketStatus.EnProceso, ticket.Status);   // sigue en proceso
        Assert.Null(ticket.ResolvedAt);
        Assert.Null(timeLog.EndTime);                                  // la visita sigue abierta
        Assert.Equal(TechnicianStatus.Ocupado, technician.Status);     // el técnico sigue ocupado
        Assert.Empty(await assertDb.MeterReadings.Where(m => m.AssetId == assetId).ToListAsync());
    }

    // ── Caso 2 (severidad 🔴): MaintenanceScheduleEvaluationJob ──────────────────────────────────
    // Antes: 1 commit para las N órdenes + 1 por cada asignación. Una caída a mitad del loop dejaba
    // órdenes Pendiente huérfanas de forma PERMANENTE — el reintento de Hangfire no las cura, porque
    // EvaluateAllDueAsync salta los cronogramas que ya tienen una orden abierta.
    [Fact]
    public async Task JobDiario_GeneraYAsignaVariasOrdenes_EsUnSoloCommit()
    {
        var dbName = Guid.NewGuid().ToString();
        const int scheduleCount = 5;

        using (var arrangeDb = TonerTestDb.CreateContext(dbName))
        {
            var city = TestEntities.City();
            var client = TestEntities.Client();
            var location = TestEntities.ClientLocation(client, city);
            var contract = TestEntities.Contract(client);
            var brand = TestEntities.AssetBrand();
            var model = TestEntities.AssetModel(brand);
            var techRole = TestEntities.Role(RoleNames.Tecnico);
            var techUser = TestEntities.User(techRole);
            var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);
            var coverage = TestEntities.Coverage(technician, city);
            arrangeDb.AddRange(city, client, location, contract, brand, model, techRole, techUser, technician, coverage);

            for (var i = 0; i < scheduleCount; i++)
            {
                var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
                var schedule = TestEntities.MaintenanceSchedule(
                    asset, contract,
                    nextGeneralDueAt: DateTime.UtcNow,           // vencido -> genera orden
                    nextGeneralDueCounter: 10_000_000,
                    nextUnitsDueAt: DateTime.UtcNow.AddYears(5),
                    nextUnitsDueCounter: 10_000_000,
                    nextConsumablesDueCounter: 10_000_000);
                arrangeDb.AddRange(asset, schedule);
            }

            await arrangeDb.SaveChangesAsync();
        }

        var spy = new SaveChangesSpyInterceptor { FailOnSaveNumber = 2 };
        using var actDb = TonerTestDb.CreateContext(dbName, spy);
        var job = new MaintenanceScheduleEvaluationJob(
            actDb,
            new MaintenanceScheduleEngine(actDb),
            TestAssignment.Create(actDb),
            new NoOpExceptionLogger(),
            NullLogger<MaintenanceScheduleEvaluationJob>.Instance,
            new TenantContextAccessor());

        var created = await job.RunAsync();

        Assert.Equal(scheduleCount, created);
        Assert.Equal(1, spy.SaveCount);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var orders = await assertDb.MaintenanceOrders.ToListAsync();
        Assert.Equal(scheduleCount, orders.Count);
        // Ninguna orden huérfana: todas asignadas, todas con su AssignmentHistory.
        Assert.All(orders, o => Assert.Equal(MaintenanceOrderStatus.Asignada, o.Status));
        Assert.All(orders, o => Assert.NotNull(o.TechnicianId));
        Assert.Equal(scheduleCount, await assertDb.AssignmentHistories.CountAsync());
    }

    // ── Caso 3 (severidad 🟠): AssetService.AddMeterReadingAsync ─────────────────────────────────
    // Antes: commit de la lectura + orden, y luego otro para la asignación. Si el segundo fallaba,
    // la orden quedaba Pendiente huérfana y bloqueaba el cronograma de ese activo para siempre.
    [Fact]
    public async Task AddMeterReadingAsync_QueGeneraOrden_EsUnSoloCommit()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid assetId;

        using (var arrangeDb = TonerTestDb.CreateContext(dbName))
        {
            var city = TestEntities.City();
            var client = TestEntities.Client();
            var location = TestEntities.ClientLocation(client, city);
            var contract = TestEntities.Contract(client);
            var brand = TestEntities.AssetBrand();
            var model = TestEntities.AssetModel(brand);
            var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
            assetId = asset.Id;
            var schedule = TestEntities.MaintenanceSchedule(
                asset, contract,
                nextGeneralDueAt: DateTime.UtcNow, nextGeneralDueCounter: 10_000_000,
                nextUnitsDueAt: DateTime.UtcNow.AddYears(5), nextUnitsDueCounter: 10_000_000,
                nextConsumablesDueCounter: 10_000_000);

            var techRole = TestEntities.Role(RoleNames.Tecnico);
            var techUser = TestEntities.User(techRole);
            var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);
            var coverage = TestEntities.Coverage(technician, city);

            arrangeDb.AddRange(city, client, location, contract, brand, model, asset, schedule, techRole, techUser, technician, coverage);
            await arrangeDb.SaveChangesAsync();
        }

        var spy = new SaveChangesSpyInterceptor { FailOnSaveNumber = 2 };
        using var actDb = TonerTestDb.CreateContext(dbName, spy);
        var scheduleEngine = new MaintenanceScheduleEngine(actDb);
        var service = new AssetService(actDb, scheduleEngine, TestAssignment.Create(actDb));
        var staff = new RequestingUser(Guid.NewGuid(), RoleNames.Administrador, null, null);

        await service.AddMeterReadingAsync(assetId, new CreateMeterReadingRequest { CounterValue = 28_000 }, staff);

        Assert.Equal(1, spy.SaveCount);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var order = await assertDb.MaintenanceOrders.SingleAsync();
        Assert.Equal(MaintenanceOrderStatus.Asignada, order.Status);
        Assert.NotNull(order.TechnicianId);
        Assert.Single(await assertDb.MeterReadings.ToListAsync());
        Assert.Single(await assertDb.AssignmentHistories.ToListAsync());
    }

    // ── Caso 4 (severidad 🟡): ServiceTicketService.CreateAsync ──────────────────────────────────
    // Antes: commit del ticket en Abierto + otro para la asignación. Si el segundo fallaba, el
    // ticket quedaba en Abierto y nada volvía a intentar asignarlo automáticamente.
    [Fact]
    public async Task CreateTicketAsync_EsUnSoloCommit()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid locationId, reporterId;

        using (var arrangeDb = TonerTestDb.CreateContext(dbName))
        {
            var role = TestEntities.Role(RoleNames.Cliente);
            var user = TestEntities.User(role);
            var city = TestEntities.City();
            var client = TestEntities.Client();
            var location = TestEntities.ClientLocation(client, city);
            locationId = location.Id;
            reporterId = user.Id;

            var techRole = TestEntities.Role(RoleNames.Tecnico);
            var techUser = TestEntities.User(techRole);
            var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);
            var coverage = TestEntities.Coverage(technician, city);

            arrangeDb.AddRange(role, user, city, client, location, techRole, techUser, technician, coverage);
            await arrangeDb.SaveChangesAsync();
        }

        var spy = new SaveChangesSpyInterceptor { FailOnSaveNumber = 2 };
        using var actDb = TonerTestDb.CreateContext(dbName, spy);
        var service = new ServiceTicketService(actDb, TestAssignment.Create(actDb));
        var staff = new RequestingUser(reporterId, RoleNames.Administrador, null, null);

        var created = await service.CreateAsync(staff, new CreateServiceTicketRequest
        {
            ClientLocationId = locationId,
            Description = "Impresora atascada"
        });

        Assert.Equal(1, spy.SaveCount);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var ticket = await assertDb.ServiceTickets.SingleAsync(t => t.Id == created.Id);
        // El ticket nunca queda en Abierto: o se asignó, o quedó SinAsignar con su historial.
        Assert.Equal(ServiceTicketStatus.Asignado, ticket.Status);
        Assert.NotNull(ticket.TechnicianId);
        Assert.Single(await assertDb.AssignmentHistories.ToListAsync());
    }

    // Siembra un ticket asignado a un técnico, con activo instalado, y hace el check-in.
    private static async Task<(Guid AssetId, Guid TechnicianId, Guid TicketId)> SeedTicketVisitAsync(string dbName)
    {
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);
        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Asignado, technicianId: technician.Id);
        ticket.AssetId = asset.Id;

        arrangeDb.AddRange(role, user, city, client, location, brand, model, asset, techRole, techUser, technician, ticket);
        await arrangeDb.SaveChangesAsync();

        using var checkInDb = TonerTestDb.CreateContext(dbName);
        await BuildCheckInService(checkInDb).CheckInAsync(technician.Id, new CheckInRequest { ServiceTicketId = ticket.Id });

        return (asset.Id, technician.Id, ticket.Id);
    }

    private sealed class NoOpExceptionLogger : IExceptionLogger
    {
        public Task LogAsync(
            string source,
            Exception exception,
            string? requestMethod = null,
            string? requestPath = null,
            int? statusCode = null,
            Guid? userId = null,
            string? userEmail = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
