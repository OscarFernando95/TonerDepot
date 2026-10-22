using Microsoft.EntityFrameworkCore;
using Toner.Application.ClientPortal;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Clients;

// El inicio del cliente: un resumen de su servicio (tickets, mantenimientos, instalaciones), dentro de su empresa y de sus
// sedes, y sin nada de contratos ni de alquiler.
public class ClientPortalServiceTests
{
    private sealed record World(
        string DbName, Client Client, ClientLocation Norte, ClientLocation Sur, Guid ClientUserId, Client Other);

    private static async Task<World> SeedAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var brand = TestEntities.AssetBrand("Ricoh");
        var model = TestEntities.AssetModel(brand, "MP 301");
        model.RentalRetirementPrintThreshold = 100; // si se filtrara al cliente, saltaría en el DTO
        var client = TestEntities.Client("Notaría 5");
        var other = TestEntities.Client("Otra");
        var norte = TestEntities.ClientLocation(client, city, "Norte");
        var sur = TestEntities.ClientLocation(client, city, "Sur");
        var otherLoc = TestEntities.ClientLocation(other, city, "Ajena");
        var role = TestEntities.Role(RoleNames.Cliente);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var clientUser = TestEntities.User(role, clientId: client.Id);
        var techUser = TestEntities.User(techRole);
        techUser.FullName = "Carlos Técnico";
        var tech = TestEntities.Technician(techUser);

        var assetN = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, norte.Id);
        var assetS = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, sur.Id);
        var pendingN = TestEntities.Asset(model, AssetLifecycleStatus.PendienteInstalacion, norte.Id);
        var assetOther = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, otherLoc.Id);

        ServiceTicket T(ClientLocation l, ServiceTicketStatus st, Asset? a, Guid? techId, int minutesAgo, Client c)
        {
            var t = TestEntities.ServiceTicket(l, clientUser, st, ServiceTicketPriority.Alta, techId);
            t.ClientId = c.Id;
            t.AssetId = a?.Id;
            t.CreatedAt = DateTime.UtcNow.AddMinutes(-minutesAgo);
            return t;
        }

        var tOpenN = T(norte, ServiceTicketStatus.Abierto, assetN, null, 10, client);
        var tInProgS = T(sur, ServiceTicketStatus.EnProceso, assetS, tech.Id, 20, client);
        var tClosedN = T(norte, ServiceTicketStatus.Cerrado, assetN, tech.Id, 5, client); // el más reciente, pero cerrado
        var tOther = T(otherLoc, ServiceTicketStatus.Abierto, assetOther, null, 1, other);

        var contract = new Contract { Id = Guid.NewGuid() };
        var schedN = TestEntities.MaintenanceSchedule(assetN, contract);
        var schedS = TestEntities.MaintenanceSchedule(assetS, contract);
        var schedOther = TestEntities.MaintenanceSchedule(assetOther, contract);
        var oPlanned = TestEntities.MaintenanceOrder(schedN, assetN, MaintenanceOrderStatus.Asignada, tech.Id, scheduledDate: DateTime.UtcNow.AddDays(20));
        oPlanned.ClientId = client.Id;
        oPlanned.PlannedAt = DateTime.UtcNow.AddDays(3);
        var oNoDate = TestEntities.MaintenanceOrder(schedS, assetS, MaintenanceOrderStatus.Pendiente, null, scheduledDate: DateTime.UtcNow.AddDays(10));
        oNoDate.ClientId = client.Id;
        var oOther = TestEntities.MaintenanceOrder(schedOther, assetOther, MaintenanceOrderStatus.Asignada, null);
        oOther.ClientId = other.Id;

        db.AddRange(city, brand, model, client, other, norte, sur, otherLoc, role, techRole, clientUser, techUser, tech,
            assetN, assetS, pendingN, assetOther, tOpenN, tInProgS, tClosedN, tOther, schedN, schedS, schedOther, oPlanned, oNoDate, oOther);
        await db.SaveChangesAsync();
        return new World(dbName, client, norte, sur, clientUser.Id, other);
    }

    private static RequestingUser Client(World w) => new(w.ClientUserId, RoleNames.Cliente, w.Client.Id, null);

    [Fact]
    public async Task ResumenDelCliente_ContadoresListasYOrden()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);

        var home = await new ClientPortalService(db).GetHomeAsync(Client(w));

        Assert.Equal("Notaría 5", home.ClientName);
        Assert.Equal(2, home.LocationCount);
        Assert.Equal(2, home.OpenTickets);           // el cerrado y el de la otra empresa no cuentan
        Assert.Equal(1, home.TicketsInProgress);
        Assert.Equal(2, home.OpenMaintenance);
        Assert.Equal(1, home.MaintenanceScheduled);        // la de Norte, con visita fijada
        Assert.Equal(0, home.MaintenanceInProgress);
        Assert.Equal(1, home.MaintenanceWithoutVisitDate);  // la de Sur
        Assert.Equal(1, home.PendingInstallations);
        Assert.Equal(0, home.InstallationsInProgress);
        Assert.Equal(2, home.InstalledAssets);
        Assert.Equal(0, home.AssetsInMaintenance);
        Assert.Equal(2, home.AssetsWithOpenTicket);         // las dos máquinas con ticket abierto (Norte y Sur)

        // Lo abierto primero (aunque el cerrado sea más reciente), y nada de otra empresa.
        Assert.Equal(new[] { "Abierto", "EnProceso", "Cerrado" }, home.RecentTickets.Select(t => t.Status));
        Assert.Equal("Ricoh MP 301", home.RecentTickets[0].Equipment);
        Assert.Equal("Carlos Técnico", home.RecentTickets[1].TechnicianName);

        // Primero la visita más cercana: la planeada para dentro de 3 días antes que la objetivo de 10.
        Assert.Equal(new[] { "Asignada", "Pendiente" }, home.UpcomingMaintenance.Select(m => m.Status));
        Assert.NotNull(home.UpcomingMaintenance[0].PlannedAt);
        Assert.Null(home.UpcomingMaintenance[1].PlannedAt);

        var install = Assert.Single(home.Installations);
        Assert.Equal("Norte", install.LocationName);
    }

    [Fact]
    public async Task ElClienteRestringido_SoloVeLoDeSusSedes()
    {
        var w = await SeedAsync();
        var restricted = await TestClientUsers.SeedAsync(w.DbName, w.Client.Id, restrictedToLocations: true, w.Norte.Id);
        using var db = TonerTestDb.CreateContext(w.DbName);

        var home = await new ClientPortalService(db).GetHomeAsync(restricted);

        Assert.Equal(1, home.LocationCount);
        Assert.Equal(1, home.OpenTickets);            // solo el de Norte
        Assert.Equal(0, home.TicketsInProgress);      // el en proceso era de Sur
        Assert.Equal(1, home.OpenMaintenance);
        Assert.Equal(0, home.MaintenanceWithoutVisitDate);
        Assert.Equal(1, home.InstalledAssets);
        Assert.All(home.RecentTickets, t => Assert.Equal("Norte", t.LocationName));
    }

    [Fact]
    public async Task RestringidoSinSedes_VeUnInicioVacio_NoElDeLaEmpresa()
    {
        var w = await SeedAsync();
        var restricted = await TestClientUsers.SeedAsync(w.DbName, w.Client.Id, restrictedToLocations: true);
        using var db = TonerTestDb.CreateContext(w.DbName);

        var home = await new ClientPortalService(db).GetHomeAsync(restricted);

        Assert.Equal(0, home.LocationCount);
        Assert.Equal(0, home.OpenTickets);
        Assert.Empty(home.RecentTickets);
        Assert.Empty(home.UpcomingMaintenance);
        Assert.Empty(home.Installations);
    }

    [Fact]
    public async Task SoloParaClientes_YConEmpresaAsociada()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);
        var service = new ClientPortalService(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetHomeAsync(new RequestingUser(Guid.NewGuid(), RoleNames.Coordinador, null, null)));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetHomeAsync(new RequestingUser(Guid.NewGuid(), RoleNames.Cliente, null, null)));
    }

    [Fact]
    public void ElResumen_NoTieneCamposDeContratoNiDeRetiroDeAlquiler()
    {
        // Barrera estructural: si alguien agrega esos datos al DTO del cliente, esta prueba obliga a decidirlo.
        var names = typeof(Toner.Application.ClientPortal.Dtos.ClientHomeDto).Assembly
            .GetTypes().Where(t => t.Namespace == "Toner.Application.ClientPortal.Dtos")
            .SelectMany(t => t.GetProperties().Select(p => p.Name));

        Assert.DoesNotContain(names, n => n.Contains("Contract", StringComparison.OrdinalIgnoreCase) || n.Contains("Retirement", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EstadoDeMaquinas_MantenimientoEnCurso_InstalacionEnCurso_YMaquinaEnMantenimiento()
    {
        var w = await SeedAsync();
        using (var arrange = TonerTestDb.CreateContext(w.DbName))
        {
            // La orden de Norte pasa a EnProceso, la máquina de Sur queda EnMantenimiento y un técnico instala la pendiente.
            (await arrange.MaintenanceOrders.FirstAsync(o => o.ClientId == w.Client.Id && o.PlannedAt != null)).Status = MaintenanceOrderStatus.EnProceso;
            var south = await arrange.Assets.FirstAsync(a => a.CurrentClientLocationId == w.Sur.Id);
            south.LifecycleStatus = AssetLifecycleStatus.EnMantenimiento;
            var pending = await arrange.Assets.FirstAsync(a => a.LifecycleStatus == AssetLifecycleStatus.PendienteInstalacion);
            var tech = await arrange.Technicians.FirstAsync();
            arrange.Add(new TimeLog { TechnicianId = tech.Id, AssetId = pending.Id, ClientId = w.Client.Id, StartTime = DateTime.UtcNow });
            await arrange.SaveChangesAsync();
        }

        using var db = TonerTestDb.CreateContext(w.DbName);
        var home = await new ClientPortalService(db).GetHomeAsync(Client(w));

        Assert.Equal(1, home.MaintenanceInProgress);
        Assert.Equal(0, home.MaintenanceScheduled);
        Assert.Equal(1, home.AssetsInMaintenance);
        Assert.Equal(1, home.InstalledAssets);
        Assert.Equal(1, home.InstallationsInProgress);
        Assert.True(Assert.Single(home.Installations).InProgress);
    }
}
