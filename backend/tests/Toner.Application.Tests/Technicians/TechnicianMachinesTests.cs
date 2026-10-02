using Toner.Application.Assets;
using Toner.Application.Common;
using Toner.Application.Maintenance;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Tickets;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Technicians;

// "Mis máquinas" del técnico: las que tiene vinculadas, con su último tóner, y los tickets pendientes de ellas.
public class TechnicianMachinesTests
{
    private sealed record Setup(string DbName, Guid TechnicianId, Guid UserId, Guid OtherTechnicianId, Guid MyAssetId, Guid OtherAssetId, Guid LocationId, Guid ClientId, Guid ReporterId, Guid TonerId, Guid LocationInvId);

    private static async Task<Setup> SeedAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var brand = TestEntities.AssetBrand("Ricoh");
        var model = TestEntities.AssetModel(brand);
        var mine = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        mine.ClientId = client.Id;
        var other = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        other.ClientId = client.Id;
        var clientRole = TestEntities.Role(RoleNames.Cliente);
        var reporter = TestEntities.User(clientRole);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var otherUser = TestEntities.User(techRole);
        var tech = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);
        var otherTech = TestEntities.Technician(otherUser, isActive: true, status: TechnicianStatus.Disponible);
        var toner = new InventoryItem { Name = "Tóner negro", Category = InventoryCategory.Toner };
        var inv = new InventoryLocation { Kind = InventoryLocationKind.Principal, Name = "Bodega" };
        db.AddRange(city, client, location, brand, model, mine, other, clientRole, reporter, techRole, techUser, otherUser, tech, otherTech, toner, inv);
        db.Add(new TechnicianAsset { TechnicianId = tech.Id, AssetId = mine.Id });
        await db.SaveChangesAsync();
        return new Setup(dbName, tech.Id, techUser.Id, otherTech.Id, mine.Id, other.Id, location.Id, client.Id, reporter.Id, toner.Id, inv.Id);
    }

    private static async Task<Guid> AddTicketAsync(Setup s, Guid assetId, Guid? technicianId, ServiceTicketStatus status)
    {
        using var db = TonerTestDb.CreateContext(s.DbName);
        var ticket = new ServiceTicket
        {
            ClientLocationId = s.LocationId, ClientId = s.ClientId, AssetId = assetId, ReportedByUserId = s.ReporterId,
            Description = "Falla", Status = status, Priority = ServiceTicketPriority.Media, TechnicianId = technicianId
        };
        db.Add(ticket);
        await db.SaveChangesAsync();
        return ticket.Id;
    }

    [Fact]
    public async Task TicketsDeMisMaquinas_SonLosPendientesDeLasVinculadas_QueNoAtiendoYo()
    {
        var s = await SeedAsync();
        var unassigned = await AddTicketAsync(s, s.MyAssetId, null, ServiceTicketStatus.SinAsignar);
        var elsewhere = await AddTicketAsync(s, s.MyAssetId, s.OtherTechnicianId, ServiceTicketStatus.Asignado);
        await AddTicketAsync(s, s.MyAssetId, s.TechnicianId, ServiceTicketStatus.Asignado);          // es mío: ya está en Mis tickets
        await AddTicketAsync(s, s.MyAssetId, s.OtherTechnicianId, ServiceTicketStatus.Resuelto);     // ya resuelto
        await AddTicketAsync(s, s.MyAssetId, null, ServiceTicketStatus.Cancelado);
        await AddTicketAsync(s, s.OtherAssetId, s.OtherTechnicianId, ServiceTicketStatus.Asignado);  // máquina que no es mía

        using var db = TonerTestDb.CreateContext(s.DbName);
        var page = await new ServiceTicketService(db, TestAssignment.Create(db)).ListForLinkedMachinesAsync(s.TechnicianId, null, null);

        Assert.Equivalent(new[] { unassigned, elsewhere }, page.Items.Select(t => t.Id), strict: true);
    }

    [Fact]
    public async Task ListaDeMaquinas_TraeElUltimoToner_ConSuContador_YLasUnidadesDe90Dias()
    {
        var s = await SeedAsync();
        using (var arrange = TonerTestDb.CreateContext(s.DbName))
        {
            InventoryMovement M(int daysAgo, int qty, long counter) => new()
            {
                InventoryItemId = s.TonerId, InventoryLocationId = s.LocationInvId, Type = InventoryMovementType.Consumo, Delta = -qty,
                AssetId = s.MyAssetId, ClientId = s.ClientId, OccurredAt = DateTime.UtcNow.AddDays(-daysAgo), CounterValue = counter
            };
            arrange.InventoryMovements.AddRange(M(200, 1, 1000), M(60, 2, 5000), M(10, 1, 9000));
            await arrange.SaveChangesAsync();
        }

        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = new AssetService(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));
        var tech = new RequestingUser(s.UserId, RoleNames.Tecnico, null, s.TechnicianId);

        var row = Assert.Single((await service.ListForMeterReadingAsync(tech, null, null)).Items);

        Assert.Equal(s.MyAssetId, row.AssetId);
        Assert.Equal(9000, row.LastTonerCounter);
        Assert.InRange(row.LastTonerAt!.Value, DateTime.UtcNow.AddDays(-11), DateTime.UtcNow.AddDays(-9));
        Assert.Equal(3, row.TonerUnitsLast90Days);   // 2 + 1; el de hace 200 días no cuenta
    }

    [Fact]
    public async Task UnaMaquinaSinToner_NoTraeDatosDeToner()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = new AssetService(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));

        var row = Assert.Single((await service.ListForMeterReadingAsync(new RequestingUser(s.UserId, RoleNames.Tecnico, null, s.TechnicianId), null, null)).Items);

        Assert.Null(row.LastTonerAt);
        Assert.Null(row.LastTonerCounter);
        Assert.Equal(0, row.TonerUnitsLast90Days);
    }
}
