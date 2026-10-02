using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Toner.Application.Realtime;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;
using Toner.Infrastructure.Realtime;

namespace Toner.Application.Tests.Realtime;

public class RealtimeChangeInterceptorTests
{
    private sealed class FakeNotifier : IRealtimeNotifier
    {
        private readonly SemaphoreSlim _signal = new(0);
        public List<EntityChange> Changes { get; } = new();
        public List<Guid> Revoked { get; } = new();
        public bool ThrowOnPublish { get; set; }

        public Task PublishAsync(IReadOnlyList<EntityChange> changes, CancellationToken cancellationToken = default)
        {
            Changes.AddRange(changes);
            _signal.Release();
            return ThrowOnPublish ? throw new InvalidOperationException("caído") : Task.CompletedTask;
        }

        public Task SessionsRevokedAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken = default)
        {
            Revoked.AddRange(sessionIds);
            _signal.Release();
            return Task.CompletedTask;
        }

        // La publicación es en segundo plano: se espera a que llegue (o se comprueba que NO llegó).
        public Task<bool> WaitAsync(int millis = 1500) => _signal.WaitAsync(millis);
    }

    private static TonerDbContext Ctx(string name, FakeNotifier notifier) =>
        TonerTestDb.CreateContext(name, new RealtimeChangeInterceptor(notifier, NullLogger<RealtimeChangeInterceptor>.Instance));

    private static (Client Client, ClientLocation Location, User Reporter, Technician TechA, Technician TechB, List<object> All) Seed()
    {
        var clientRole = TestEntities.Role(RoleNames.Cliente);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var reporter = TestEntities.User(clientRole);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var techUserA = TestEntities.User(techRole);
        var techUserB = TestEntities.User(techRole);
        var techA = TestEntities.Technician(techUserA);
        var techB = TestEntities.Technician(techUserB);
        var all = new List<object> { clientRole, techRole, reporter, city, client, location, techUserA, techUserB, techA, techB };
        return (client, location, reporter, techA, techB, all);
    }

    [Fact]
    public async Task TicketNuevo_PublicaCreatedConClienteYTecnico()
    {
        var seed = Seed();
        var dbName = Guid.NewGuid().ToString();
        using (var arrange = TonerTestDb.CreateContext(dbName))
        {
            arrange.AddRange(seed.All);
            await arrange.SaveChangesAsync();
        }

        var notifier = new FakeNotifier();
        using var db = Ctx(dbName, notifier);
        var ticket = TestEntities.ServiceTicket(seed.Location, seed.Reporter, ServiceTicketStatus.Asignado, technicianId: seed.TechA.Id);
        ticket.ClientId = seed.Client.Id;
        db.Add(ticket);
        await db.SaveChangesAsync();

        Assert.True(await notifier.WaitAsync());
        var change = Assert.Single(notifier.Changes);
        Assert.Equal(("Ticket", ticket.Id, "created"), (change.Entity, change.Id, change.Action));
        Assert.Equal(seed.Client.Id, change.ClientId);
        Assert.Equal(new[] { seed.TechA.Id }, change.TechnicianIds);
    }

    [Fact]
    public async Task ReasignarTicket_AvisaAlTecnicoNuevoYAlAnterior()
    {
        var seed = Seed();
        var dbName = Guid.NewGuid().ToString();
        Guid ticketId;
        using (var arrange = TonerTestDb.CreateContext(dbName))
        {
            var ticket = TestEntities.ServiceTicket(seed.Location, seed.Reporter, ServiceTicketStatus.Asignado, technicianId: seed.TechA.Id);
            ticket.ClientId = seed.Client.Id;
            ticketId = ticket.Id;
            arrange.AddRange(seed.All);
            arrange.Add(ticket);
            await arrange.SaveChangesAsync();
        }

        var notifier = new FakeNotifier();
        using var db = Ctx(dbName, notifier);
        (await db.ServiceTickets.SingleAsync(t => t.Id == ticketId)).TechnicianId = seed.TechB.Id;
        await db.SaveChangesAsync();

        Assert.True(await notifier.WaitAsync());
        var change = Assert.Single(notifier.Changes);
        Assert.Equal("updated", change.Action);
        Assert.Equivalent(new[] { seed.TechA.Id, seed.TechB.Id }, change.TechnicianIds, strict: true);
    }

    [Fact]
    public async Task CambiosEnTablasNoRelevantes_NoPublicanNada()
    {
        var notifier = new FakeNotifier();
        using var db = Ctx(Guid.NewGuid().ToString(), notifier);
        db.Add(TestEntities.City("Neiva", "Huila"));
        await db.SaveChangesAsync();

        Assert.False(await notifier.WaitAsync(400));
        Assert.Empty(notifier.Changes);
    }

    [Fact]
    public async Task SesionRevocada_SePublica_PeroTocarLastSeenNo()
    {
        var seed = Seed();
        var dbName = Guid.NewGuid().ToString();
        var session = new UserSession { UserId = seed.Reporter.Id, ExpiresAt = DateTime.UtcNow.AddHours(1) };
        using (var arrange = TonerTestDb.CreateContext(dbName))
        {
            arrange.AddRange(seed.All);
            arrange.Add(session);
            await arrange.SaveChangesAsync();
        }

        var notifier = new FakeNotifier();
        using var db = Ctx(dbName, notifier);
        var tracked = await db.UserSessions.SingleAsync();
        tracked.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Assert.False(await notifier.WaitAsync(400), "tocar LastSeenAt no debe generar avisos");

        tracked.RevokedAt = DateTime.UtcNow;
        tracked.RevokedReason = "Logout";
        await db.SaveChangesAsync();

        Assert.True(await notifier.WaitAsync());
        Assert.Equal(new[] { session.Id }, notifier.Revoked);
    }

    [Fact]
    public async Task SiElAvisoFalla_ElGuardadoIgualSeConfirma()
    {
        var seed = Seed();
        var dbName = Guid.NewGuid().ToString();
        var notifier = new FakeNotifier { ThrowOnPublish = true };
        using (var db = Ctx(dbName, notifier))
        {
            db.AddRange(seed.All);
            var ticket = TestEntities.ServiceTicket(seed.Location, seed.Reporter);
            ticket.ClientId = seed.Client.Id;
            db.Add(ticket);
            await db.SaveChangesAsync(); // no debe lanzar
        }

        using var check = TonerTestDb.CreateContext(dbName);
        Assert.Equal(1, await check.ServiceTickets.CountAsync());
    }

    [Fact]
    public async Task AsignarUnaZonaAUnTecnico_AvisaAEseTecnico()
    {
        var seed = Seed();
        var dbName = Guid.NewGuid().ToString();
        var zone = new Zone { Name = "Zona Sur" };
        using (var arrange = TonerTestDb.CreateContext(dbName))
        {
            arrange.AddRange(seed.All);
            arrange.Add(zone);
            await arrange.SaveChangesAsync();
        }

        var notifier = new FakeNotifier();
        using var db = Ctx(dbName, notifier);
        db.TechnicianZones.Add(new TechnicianZone { TechnicianId = seed.TechA.Id, ZoneId = zone.Id });
        await db.SaveChangesAsync();

        Assert.True(await notifier.WaitAsync());
        var change = Assert.Single(notifier.Changes);
        Assert.Equal("Technician", change.Entity);
        Assert.Equal(seed.TechA.Id, change.Id);
        Assert.Equal(new[] { seed.TechA.Id }, change.TechnicianIds);
    }

    [Fact]
    public async Task MoverUnMunicipioDeZona_AvisaDeLaZonaNuevaYDeLaAnterior()
    {
        var seed = Seed();
        var dbName = Guid.NewGuid().ToString();
        var from = new Zone { Name = "Zona Origen" };
        var to = new Zone { Name = "Zona Destino" };
        var city = TestEntities.City("Neiva", "Huila");
        city.ZoneId = from.Id;
        using (var arrange = TonerTestDb.CreateContext(dbName))
        {
            arrange.AddRange(seed.All);
            arrange.AddRange(from, to, city);
            await arrange.SaveChangesAsync();
        }

        var notifier = new FakeNotifier();
        using var db = Ctx(dbName, notifier);
        (await db.Cities.SingleAsync(c => c.Id == city.Id)).ZoneId = to.Id;
        await db.SaveChangesAsync();

        Assert.True(await notifier.WaitAsync());
        Assert.Equivalent(new[] { from.Id, to.Id }, notifier.Changes.Where(c => c.Entity == "Zone").Select(c => c.Id), strict: true);
    }

    [Fact]
    public async Task VincularUnActivoAUnTecnico_AvisaSoloAEseTecnico()
    {
        var seed = Seed();
        var dbName = Guid.NewGuid().ToString();
        Guid assetId;
        using (var arrange = TonerTestDb.CreateContext(dbName))
        {
            var brand = TestEntities.AssetBrand();
            var model = TestEntities.AssetModel(brand);
            var asset = TestEntities.Asset(model);
            assetId = asset.Id;
            arrange.AddRange(seed.All);
            arrange.AddRange(brand, model, asset);
            await arrange.SaveChangesAsync();
        }

        var notifier = new FakeNotifier();
        using var db = Ctx(dbName, notifier);
        db.TechnicianAssets.Add(new TechnicianAsset { TechnicianId = seed.TechA.Id, AssetId = assetId });
        await db.SaveChangesAsync();

        Assert.True(await notifier.WaitAsync());
        var change = Assert.Single(notifier.Changes);
        Assert.Equal("TechnicianAsset", change.Entity);
        Assert.Equal(new[] { seed.TechA.Id }, change.TechnicianIds);
    }
}
