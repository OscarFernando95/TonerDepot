using Toner.Application.Assets;
using Toner.Application.Clients;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Maintenance;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Tickets;
using Toner.Application.Tickets.Dtos;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Clients;

// Un usuario Cliente con acceso a ciertas sedes ve y reporta SOLO en ellas: tickets, activos, mantenimientos y la lista de sedes.
public class ClientLocationScopeTests
{
    private sealed record World(
        string DbName, Client Client, ClientLocation Norte, ClientLocation Sur,
        Asset AssetNorte, Asset AssetSur, ServiceTicket TicketNorte, ServiceTicket TicketSur,
        MaintenanceOrder OrderNorte, MaintenanceOrder OrderSur, Guid ReporterId);

    private static async Task<World> SeedAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var client = TestEntities.Client("Empresa");
        var norte = TestEntities.ClientLocation(client, city, "Norte");
        var sur = TestEntities.ClientLocation(client, city, "Sur");
        var assetN = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, norte.Id);
        var assetS = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, sur.Id);
        var role = TestEntities.Role(RoleNames.Cliente);
        var reporter = TestEntities.User(role, clientId: client.Id);
        var ticketN = TestEntities.ServiceTicket(norte, reporter);
        ticketN.ClientId = client.Id;
        var ticketS = TestEntities.ServiceTicket(sur, reporter);
        ticketS.ClientId = client.Id;
        var contract = new Contract { Id = Guid.NewGuid() };
        var scheduleN = TestEntities.MaintenanceSchedule(assetN, contract);
        var scheduleS = TestEntities.MaintenanceSchedule(assetS, contract);
        var orderN = TestEntities.MaintenanceOrder(scheduleN, assetN, MaintenanceOrderStatus.Asignada);
        orderN.ClientId = client.Id;
        var orderS = TestEntities.MaintenanceOrder(scheduleS, assetS, MaintenanceOrderStatus.Asignada);
        orderS.ClientId = client.Id;
        db.AddRange(city, brand, model, client, norte, sur, assetN, assetS, role, reporter, ticketN, ticketS,
            scheduleN, scheduleS, orderN, orderS);
        await db.SaveChangesAsync();
        return new World(dbName, client, norte, sur, assetN, assetS, ticketN, ticketS, orderN, orderS, reporter.Id);
    }

    private static async Task<RequestingUser> OnlyNorteAsync(World w) =>
        await TestClientUsers.SeedAsync(w.DbName, w.Client.Id, restrictedToLocations: true, w.Norte.Id);

    private static ServiceTicketService Tickets(Infrastructure.Persistence.TonerDbContext db) => new(db, TestAssignment.Create(db));
    private static AssetService Assets(Infrastructure.Persistence.TonerDbContext db) =>
        new(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));
    private static MaintenanceOrderService Orders(Infrastructure.Persistence.TonerDbContext db) =>
        new(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));

    [Fact]
    public async Task Tickets_ElClienteRestringidoVeSoloLosDeSuSede_Lista_Detalle_YAlReportar()
    {
        var w = await SeedAsync();
        var user = await OnlyNorteAsync(w);
        using var db = TonerTestDb.CreateContext(w.DbName);
        var service = Tickets(db);

        Assert.Equal(w.TicketNorte.Id, Assert.Single((await service.ListAsync(user, null, null)).Items).Id);
        Assert.Equal(w.TicketNorte.Id, (await service.GetByIdAsync(user, w.TicketNorte.Id)).Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetByIdAsync(user, w.TicketSur.Id));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateAsync(user, new CreateServiceTicketRequest { ClientLocationId = w.Sur.Id, Description = "No debería poder" }));
        var created = await service.CreateAsync(user, new CreateServiceTicketRequest { ClientLocationId = w.Norte.Id, Description = "Sí en su sede" });
        Assert.Equal(w.Norte.Id, created.ClientLocationId);
    }

    [Fact]
    public async Task Activos_ElClienteRestringidoVeSoloLosDeSuSede()
    {
        var w = await SeedAsync();
        var user = await OnlyNorteAsync(w);
        using var db = TonerTestDb.CreateContext(w.DbName);
        var service = Assets(db);

        Assert.Equal(w.AssetNorte.Id, Assert.Single((await service.ListAsync(user, null, null)).Items).Id);
        Assert.Equal(w.AssetNorte.Id, (await service.GetByIdAsync(user, w.AssetNorte.Id)).Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetByIdAsync(user, w.AssetSur.Id));
    }

    [Fact]
    public async Task Mantenimientos_ElClienteRestringidoVeSoloLosDeSuSede()
    {
        var w = await SeedAsync();
        var user = await OnlyNorteAsync(w);
        using var db = TonerTestDb.CreateContext(w.DbName);
        var service = Orders(db);

        Assert.Equal(w.OrderNorte.Id, Assert.Single((await service.ListAsync(user, null, null)).Items).Id);
        Assert.Equal(w.OrderNorte.Id, (await service.GetByIdAsync(user, w.OrderNorte.Id)).Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetByIdAsync(user, w.OrderSur.Id));
    }

    [Fact]
    public async Task Sedes_LaListaDelClienteRestringidoTraeSoloLasSuyas()
    {
        var w = await SeedAsync();
        var user = await OnlyNorteAsync(w);
        using var db = TonerTestDb.CreateContext(w.DbName);

        var locations = await new ClientLocationService(db).ListForClientUserAsync(user);

        Assert.Equal(new[] { "Norte" }, locations.Select(l => l.Name));
    }

    [Fact]
    public async Task SinRestriccion_ElClienteVeTodasLasSedesDeSuEmpresa()
    {
        var w = await SeedAsync();
        var user = await TestClientUsers.SeedAsync(w.DbName, w.Client.Id);
        using var db = TonerTestDb.CreateContext(w.DbName);

        Assert.Equal(2, (await Tickets(db).ListAsync(user, null, null)).Items.Count);
        Assert.Equal(2, (await Assets(db).ListAsync(user, null, null)).Items.Count);
        Assert.Equal(2, (await Orders(db).ListAsync(user, null, null)).Items.Count);
        Assert.Equal(2, (await new ClientLocationService(db).ListForClientUserAsync(user)).Count);
    }

    [Fact]
    public async Task Restringido_SinNingunaSede_NoVeNada_FallaCerrado()
    {
        var w = await SeedAsync();
        var user = await TestClientUsers.SeedAsync(w.DbName, w.Client.Id, restrictedToLocations: true);
        using var db = TonerTestDb.CreateContext(w.DbName);

        Assert.Empty((await Tickets(db).ListAsync(user, null, null)).Items);
        Assert.Empty((await Assets(db).ListAsync(user, null, null)).Items);
        Assert.Empty((await Orders(db).ListAsync(user, null, null)).Items);
        Assert.Empty(await new ClientLocationService(db).ListForClientUserAsync(user));
    }

    [Fact]
    public async Task UnaFilaDeOtraEmpresa_NuncaAmplíaElAlcance()
    {
        var w = await SeedAsync();
        var user = await TestClientUsers.SeedAsync(w.DbName, w.Client.Id, restrictedToLocations: true, w.Norte.Id);
        using (var arrange = TonerTestDb.CreateContext(w.DbName))
        {
            // Fila "colada" de otra empresa que apunta a la sede Sur: no cuenta.
            arrange.Add(new UserClientLocation { UserId = user.UserId, ClientLocationId = w.Sur.Id, ClientId = Guid.NewGuid() });
            await arrange.SaveChangesAsync();
        }

        using var db = TonerTestDb.CreateContext(w.DbName);
        Assert.Equal(new[] { w.Norte.Id }, await db.AllowedLocationIdsAsync(user));
    }

    [Fact]
    public async Task ElCambioDeSedesSurteEfectoDeInmediato_SinVolverAIniciarSesion()
    {
        var w = await SeedAsync();
        var user = await OnlyNorteAsync(w);
        using (var before = TonerTestDb.CreateContext(w.DbName))
        {
            Assert.Single((await Tickets(before).ListAsync(user, null, null)).Items);
        }

        using (var change = TonerTestDb.CreateContext(w.DbName))
        {
            change.Add(new UserClientLocation { UserId = user.UserId, ClientLocationId = w.Sur.Id, ClientId = w.Client.Id });
            await change.SaveChangesAsync();
        }

        using var after = TonerTestDb.CreateContext(w.DbName);
        Assert.Equal(2, (await Tickets(after).ListAsync(user, null, null)).Items.Count);
    }

    [Fact]
    public async Task StaffYTecnico_NoTienenAlcancePorSede()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);

        Assert.Null(await db.AllowedLocationIdsAsync(new RequestingUser(Guid.NewGuid(), RoleNames.Coordinador, null, null)));
        Assert.Null(await db.AllowedLocationIdsAsync(new RequestingUser(Guid.NewGuid(), RoleNames.Tecnico, null, Guid.NewGuid())));
    }
}
