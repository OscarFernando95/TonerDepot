using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Toner.Application.Push;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;
using Toner.Infrastructure.Push;

namespace Toner.Application.Tests.Push;

public class PushChangeInterceptorTests
{
    private sealed class FakePushNotifier : IPushNotifier
    {
        private readonly SemaphoreSlim _signal = new(0);
        public List<(Guid[] TechnicianIds, PushMessage Message)> ToTechnicians { get; } = new();
        public List<PushMessage> ToStaff { get; } = new();

        public Task NotifyTechniciansAsync(IReadOnlyCollection<Guid> technicianIds, PushMessage message, CancellationToken cancellationToken = default)
        {
            ToTechnicians.Add((technicianIds.ToArray(), message));
            _signal.Release();
            return Task.CompletedTask;
        }

        public Task NotifyStaffAsync(PushMessage message, CancellationToken cancellationToken = default)
        {
            ToStaff.Add(message);
            _signal.Release();
            return Task.CompletedTask;
        }

        public Task<bool> WaitAsync(int millis = 1500) => _signal.WaitAsync(millis);
    }

    private static TonerDbContext Ctx(string name, FakePushNotifier notifier) =>
        TonerTestDb.CreateContext(name, new PushChangeInterceptor(() => notifier, NullLogger<PushChangeInterceptor>.Instance));

    private static (ClientLocation Location, User Reporter, Technician TechA, Technician TechB, List<object> All) Seed()
    {
        var clientRole = TestEntities.Role(RoleNames.Cliente);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var reporter = TestEntities.User(clientRole);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var userA = TestEntities.User(techRole);
        var userB = TestEntities.User(techRole);
        var techA = TestEntities.Technician(userA);
        var techB = TestEntities.Technician(userB);
        return (location, reporter, techA, techB, new List<object> { clientRole, techRole, reporter, city, client, location, userA, userB, techA, techB });
    }

    private static async Task<(string Db, Guid TicketId)> SeedTicket(
        (ClientLocation Location, User Reporter, Technician TechA, Technician TechB, List<object> All) seed,
        ServiceTicketStatus status, Guid? technicianId)
    {
        var db = Guid.NewGuid().ToString();
        using var arrange = TonerTestDb.CreateContext(db);
        var ticket = TestEntities.ServiceTicket(seed.Location, seed.Reporter, status, technicianId: technicianId);
        arrange.AddRange(seed.All);
        arrange.Add(ticket);
        await arrange.SaveChangesAsync();
        return (db, ticket.Id);
    }

    [Fact]
    public async Task TicketNuevoYaAsignado_AvisaAlTecnico_ConMensajeGenericoYSoloIds()
    {
        var seed = Seed();
        var dbName = Guid.NewGuid().ToString();
        using (var arrange = TonerTestDb.CreateContext(dbName))
        {
            arrange.AddRange(seed.All);
            await arrange.SaveChangesAsync();
        }

        var notifier = new FakePushNotifier();
        using var db = Ctx(dbName, notifier);
        var ticket = TestEntities.ServiceTicket(seed.Location, seed.Reporter, ServiceTicketStatus.Asignado, technicianId: seed.TechA.Id);
        db.Add(ticket);
        await db.SaveChangesAsync();

        Assert.True(await notifier.WaitAsync());
        var (techs, message) = Assert.Single(notifier.ToTechnicians);
        Assert.Equal(new[] { seed.TechA.Id }, techs);
        Assert.Equal("Nuevo ticket asignado", message.Title);
        Assert.Equivalent(new Dictionary<string, string>
        {
            ["type"] = "ticket", ["id"] = ticket.Id.ToString(), ["event"] = "assigned"
        }, message.Data, strict: true);
        // Lo visible nunca lleva datos del servicio (puede verse con la pantalla bloqueada).
        Assert.DoesNotContain("Impresora", message.Title + message.Body);
        Assert.Empty(notifier.ToStaff);
    }

    [Fact]
    public async Task AsignarUnTicketSinTecnico_AvisaAlTecnicoNuevo()
    {
        var seed = Seed();
        var (dbName, ticketId) = await SeedTicket(seed, ServiceTicketStatus.SinAsignar, null);

        var notifier = new FakePushNotifier();
        using var db = Ctx(dbName, notifier);
        var ticket = await db.ServiceTickets.SingleAsync(t => t.Id == ticketId);
        ticket.TechnicianId = seed.TechB.Id;
        ticket.Status = ServiceTicketStatus.Asignado;
        await db.SaveChangesAsync();

        Assert.True(await notifier.WaitAsync());
        var (techs, message) = Assert.Single(notifier.ToTechnicians);
        Assert.Equal(new[] { seed.TechB.Id }, techs);
        Assert.Equal("assigned", message.Data["event"]);
    }

    [Fact]
    public async Task Reasignar_AvisaAlNuevoQueSeLoAsignaronYAlAnteriorQueYaNoLoTiene()
    {
        var seed = Seed();
        var (dbName, ticketId) = await SeedTicket(seed, ServiceTicketStatus.Asignado, seed.TechA.Id);

        var notifier = new FakePushNotifier();
        using var db = Ctx(dbName, notifier);
        (await db.ServiceTickets.SingleAsync(t => t.Id == ticketId)).TechnicianId = seed.TechB.Id;
        await db.SaveChangesAsync();

        Assert.True(await notifier.WaitAsync());
        Assert.True(await notifier.WaitAsync());
        Assert.Equivalent(new[]
        {
            (seed.TechB.Id, "assigned"),
            (seed.TechA.Id, "removed")
        }, notifier.ToTechnicians.Select(n => (n.TechnicianIds.Single(), n.Message.Data["event"])), strict: true);
    }

    [Fact]
    public async Task CancelarUnTicketAsignado_AvisaAlTecnico()
    {
        var seed = Seed();
        var (dbName, ticketId) = await SeedTicket(seed, ServiceTicketStatus.Asignado, seed.TechA.Id);

        var notifier = new FakePushNotifier();
        using var db = Ctx(dbName, notifier);
        (await db.ServiceTickets.SingleAsync(t => t.Id == ticketId)).Status = ServiceTicketStatus.Cancelado;
        await db.SaveChangesAsync();

        Assert.True(await notifier.WaitAsync());
        var (techs, message) = Assert.Single(notifier.ToTechnicians);
        Assert.Equal(new[] { seed.TechA.Id }, techs);
        Assert.Equal("cancelled", message.Data["event"]);
    }

    [Fact]
    public async Task TicketQueQuedaSinAsignar_AvisaAlStaff_NoAlTecnico()
    {
        var seed = Seed();
        var (dbName, ticketId) = await SeedTicket(seed, ServiceTicketStatus.Abierto, null);

        var notifier = new FakePushNotifier();
        using var db = Ctx(dbName, notifier);
        (await db.ServiceTickets.SingleAsync(t => t.Id == ticketId)).Status = ServiceTicketStatus.SinAsignar;
        await db.SaveChangesAsync();

        Assert.True(await notifier.WaitAsync());
        var message = Assert.Single(notifier.ToStaff);
        Assert.Equal("unassigned", message.Data["event"]);
        Assert.Empty(notifier.ToTechnicians);
    }

    [Fact]
    public async Task OrdenAsignada_AvisaAlTecnico_ConTipoOrder()
    {
        var seed = Seed();
        var dbName = Guid.NewGuid().ToString();
        var model = TestEntities.AssetModel(TestEntities.AssetBrand());
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(asset, new Contract { Id = Guid.NewGuid() });
        using (var arrange = TonerTestDb.CreateContext(dbName))
        {
            arrange.AddRange(seed.All);
            arrange.AddRange(model, asset, schedule);
            await arrange.SaveChangesAsync();
        }

        var notifier = new FakePushNotifier();
        using var db = Ctx(dbName, notifier);
        var order = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Asignada, seed.TechA.Id);
        db.Add(order);
        await db.SaveChangesAsync();

        Assert.True(await notifier.WaitAsync());
        var (_, message) = Assert.Single(notifier.ToTechnicians);
        Assert.Equal("Nueva orden de mantenimiento", message.Title);
        Assert.Equal("order", message.Data["type"]);
    }

    [Fact]
    public async Task CambiosQueNoTocanLaAsignacion_NoAvisanNada()
    {
        var seed = Seed();
        var (dbName, ticketId) = await SeedTicket(seed, ServiceTicketStatus.Asignado, seed.TechA.Id);

        var notifier = new FakePushNotifier();
        using var db = Ctx(dbName, notifier);
        var ticket = await db.ServiceTickets.SingleAsync(t => t.Id == ticketId);
        ticket.Description = "Otra descripción";
        ticket.Status = ServiceTicketStatus.EnProceso;
        await db.SaveChangesAsync();

        Assert.False(await notifier.WaitAsync(400));
        Assert.Empty(notifier.ToTechnicians);
        Assert.Empty(notifier.ToStaff);
    }

    [Fact]
    public async Task SiElEnvioFalla_ElGuardadoYaConfirmadoNoSeRompe()
    {
        var seed = Seed();
        var dbName = Guid.NewGuid().ToString();
        using (var arrange = TonerTestDb.CreateContext(dbName))
        {
            arrange.AddRange(seed.All);
            await arrange.SaveChangesAsync();
        }

        using var db = TonerTestDb.CreateContext(dbName,
            new PushChangeInterceptor(() => throw new InvalidOperationException("FCM caído"), NullLogger<PushChangeInterceptor>.Instance));
        var ticket = TestEntities.ServiceTicket(seed.Location, seed.Reporter, ServiceTicketStatus.Asignado, technicianId: seed.TechA.Id);
        db.Add(ticket);
        await db.SaveChangesAsync();

        using var check = TonerTestDb.CreateContext(dbName);
        Assert.True(await check.ServiceTickets.AnyAsync(t => t.Id == ticket.Id));
    }
}
