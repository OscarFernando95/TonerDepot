using Microsoft.EntityFrameworkCore;
using Toner.Application.Technicians;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Technicians;

// Inicio del técnico: estado, visita en curso, agenda ordenada y stock bajo de su zona.
public class TechnicianHomeServiceTests
{
    private sealed record Setup(string DbName, Guid TechnicianId, Guid OtherTechnicianId, Guid LocationId, Guid ZoneId, Guid ZoneLocationId, Guid AssetId, Guid ReporterId, Guid ClientId);

    private static async Task<Setup> SeedAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var zone = new Zone { Name = "Sur" };
        var zoneLocation = new InventoryLocation { Kind = InventoryLocationKind.Zona, Zone = zone };
        var city = TestEntities.City("Pitalito", "Huila");
        city.ZoneId = zone.Id;
        var client = TestEntities.Client("Electrohuila");
        var location = TestEntities.ClientLocation(client, city, "Sede Sur");
        location.Latitude = 1.85;
        location.Longitude = -76.05;
        var brand = TestEntities.AssetBrand("Ricoh");
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        asset.ClientId = client.Id;
        var clientRole = TestEntities.Role(RoleNames.Cliente);
        var reporter = TestEntities.User(clientRole);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var otherUser = TestEntities.User(techRole);
        var tech = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);
        var other = TestEntities.Technician(otherUser, isActive: true, status: TechnicianStatus.Disponible);
        db.AddRange(zone, zoneLocation, city, client, location, brand, model, asset, clientRole, reporter, techRole, techUser, otherUser, tech, other);
        db.Add(new TechnicianZone { TechnicianId = tech.Id, ZoneId = zone.Id });
        await db.SaveChangesAsync();
        return new Setup(dbName, tech.Id, other.Id, location.Id, zone.Id, zoneLocation.Id, asset.Id, reporter.Id, client.Id);
    }

    private static TechnicianHomeService Service(Infrastructure.Persistence.TonerDbContext db, TimeProvider? time = null) =>
        new(db, TestCalendar.For(db), time ?? FixedTimeProvider.WorkingHours);

    private static async Task<ServiceTicket> AddTicketAsync(
        Setup s, Guid? technicianId, ServiceTicketStatus status, ServiceTicketPriority priority, DateTime createdAt, string description = "Falla")
    {
        using var db = TonerTestDb.CreateContext(s.DbName);
        var ticket = new ServiceTicket
        {
            ClientLocationId = s.LocationId, ClientId = s.ClientId, AssetId = s.AssetId, ReportedByUserId = s.ReporterId,
            Description = description, Status = status, Priority = priority, TechnicianId = technicianId, CreatedAt = createdAt
        };
        db.Add(ticket);
        await db.SaveChangesAsync();
        return ticket;
    }

    [Fact]
    public async Task Agenda_PoneLoEnCursoPrimero_LuegoPrioridad_YALaMismaPrioridadLoMasAntiguo()
    {
        var s = await SeedAsync();
        var now = DateTime.UtcNow;
        var low = await AddTicketAsync(s, s.TechnicianId, ServiceTicketStatus.Asignado, ServiceTicketPriority.Baja, now.AddDays(-5));
        var highNew = await AddTicketAsync(s, s.TechnicianId, ServiceTicketStatus.Asignado, ServiceTicketPriority.Alta, now.AddHours(-1));
        var highOld = await AddTicketAsync(s, s.TechnicianId, ServiceTicketStatus.Asignado, ServiceTicketPriority.Alta, now.AddDays(-2));
        var inProgress = await AddTicketAsync(s, s.TechnicianId, ServiceTicketStatus.EnProceso, ServiceTicketPriority.Baja, now.AddDays(-9));
        var critical = await AddTicketAsync(s, s.TechnicianId, ServiceTicketStatus.Asignado, ServiceTicketPriority.Critica, now.AddMinutes(-5));

        using var db = TonerTestDb.CreateContext(s.DbName);
        var home = await Service(db).GetAsync(s.TechnicianId);

        Assert.Equal(new[] { inProgress.Id, critical.Id, highOld.Id, highNew.Id, low.Id }, home.Agenda.Select(j => j.Id));
        Assert.True(home.Agenda[0].InProgress);
    }

    [Fact]
    public async Task Agenda_SoloTraeLoAsignadoAlTecnico_YEnEstadosActivos()
    {
        var s = await SeedAsync();
        var now = DateTime.UtcNow;
        var mine = await AddTicketAsync(s, s.TechnicianId, ServiceTicketStatus.Asignado, ServiceTicketPriority.Media, now);
        await AddTicketAsync(s, s.OtherTechnicianId, ServiceTicketStatus.Asignado, ServiceTicketPriority.Alta, now);
        await AddTicketAsync(s, s.TechnicianId, ServiceTicketStatus.Resuelto, ServiceTicketPriority.Alta, now);
        await AddTicketAsync(s, null, ServiceTicketStatus.SinAsignar, ServiceTicketPriority.Alta, now);

        using var db = TonerTestDb.CreateContext(s.DbName);
        var job = Assert.Single((await Service(db).GetAsync(s.TechnicianId)).Agenda);

        Assert.Equal(mine.Id, job.Id);
        Assert.Equal("Ticket", job.Kind);
        Assert.StartsWith("Ricoh ", job.Title);
        Assert.Equal("Falla", job.Summary);
        Assert.Equal("Electrohuila", job.ClientName);
        Assert.Equal("Sede Sur", job.LocationName);
        Assert.Equal("Pitalito", job.CityName);
        Assert.Equal(1.85, job.Latitude);
        Assert.Equal(-76.05, job.Longitude);
    }

    [Fact]
    public async Task Agenda_IncluyeLasOrdenesAsignadas_ComoPrioridadMedia()
    {
        var s = await SeedAsync();
        Guid orderId;
        using (var arrange = TonerTestDb.CreateContext(s.DbName))
        {
            var client = await arrange.Clients.SingleAsync();
            var contract = TestEntities.Contract(client);
            var asset = await arrange.Assets.SingleAsync();
            var schedule = TestEntities.MaintenanceSchedule(asset, contract);
            schedule.ClientId = client.Id;
            var order = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Asignada, technicianId: s.TechnicianId);
            order.ClientId = client.Id;
            arrange.AddRange(contract, schedule, order);
            await arrange.SaveChangesAsync();
            orderId = order.Id;
        }

        using var db = TonerTestDb.CreateContext(s.DbName);
        var job = Assert.Single((await Service(db).GetAsync(s.TechnicianId)).Agenda);

        Assert.Equal(orderId, job.Id);
        Assert.Equal("Orden", job.Kind);
        Assert.Equal("Media", job.Priority);
        Assert.Equal("Sede Sur", job.LocationName);
    }

    [Fact]
    public async Task VisitaEnCurso_ApareceConSuHoraDeInicio_YElTicketSigueEnLaAgenda()
    {
        var s = await SeedAsync();
        var ticket = await AddTicketAsync(s, s.TechnicianId, ServiceTicketStatus.EnProceso, ServiceTicketPriority.Media, DateTime.UtcNow.AddHours(-3));
        var started = DateTime.UtcNow.AddMinutes(-25);
        using (var arrange = TonerTestDb.CreateContext(s.DbName))
        {
            arrange.Add(new TimeLog { TechnicianId = s.TechnicianId, ServiceTicketId = ticket.Id, StartTime = started });
            await arrange.SaveChangesAsync();
        }

        using var db = TonerTestDb.CreateContext(s.DbName);
        var home = await Service(db).GetAsync(s.TechnicianId);

        Assert.Equal(ticket.Id, home.ActiveVisit!.Id);
        Assert.Equal(started, home.ActiveVisitStartedAt);
    }

    [Fact]
    public async Task VisitaEnCurso_DeUnaInstalacion_SeArmaDesdeElActivo()
    {
        var s = await SeedAsync();
        using (var arrange = TonerTestDb.CreateContext(s.DbName))
        {
            arrange.Add(new TimeLog { TechnicianId = s.TechnicianId, AssetId = s.AssetId, StartTime = DateTime.UtcNow.AddMinutes(-10) });
            await arrange.SaveChangesAsync();
        }

        using var db = TonerTestDb.CreateContext(s.DbName);
        var visit = (await Service(db).GetAsync(s.TechnicianId)).ActiveVisit!;

        Assert.Equal("Instalación", visit.Kind);
        Assert.Equal("Sede Sur", visit.LocationName);
    }

    [Fact]
    public async Task Hoy_SumaLasVisitasCerradasYSusMinutos_SinContarLasDeAyer()
    {
        var s = await SeedAsync();
        var now = FixedTimeProvider.WorkingHours.GetUtcNow().UtcDateTime;
        using (var arrange = TonerTestDb.CreateContext(s.DbName))
        {
            arrange.AddRange(
                new TimeLog { TechnicianId = s.TechnicianId, StartTime = now.AddMinutes(-90), EndTime = now.AddMinutes(-30) },   // 60 min hoy
                new TimeLog { TechnicianId = s.TechnicianId, StartTime = now.AddMinutes(-25), EndTime = now.AddMinutes(-5) },    // 20 min hoy
                new TimeLog { TechnicianId = s.TechnicianId, StartTime = now.AddDays(-1), EndTime = now.AddDays(-1).AddHours(2) }, // ayer
                new TimeLog { TechnicianId = s.OtherTechnicianId, StartTime = now.AddMinutes(-60), EndTime = now.AddMinutes(-10) });
            await arrange.SaveChangesAsync();
        }

        using var db = TonerTestDb.CreateContext(s.DbName);
        var home = await Service(db).GetAsync(s.TechnicianId);

        Assert.Equal(2, home.VisitsClosedToday);
        Assert.Equal(80, home.MinutesWorkedToday);
    }

    [Fact]
    public async Task Estado_EnHorarioHabilMuestraLaJornada_YUnSabadoNoTrabaja()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        var weekday = await Service(db, FixedTimeProvider.WorkingHours).GetAsync(s.TechnicianId);
        Assert.True(weekday.IsWorkingNow);
        Assert.NotNull(weekday.TodayShift);
        Assert.Contains("–", weekday.TodayShift);

        var saturday = await Service(db, FixedTimeProvider.Weekend).GetAsync(s.TechnicianId);
        Assert.False(saturday.IsWorkingNow);
        Assert.Null(saturday.TodayShift);
    }

    [Fact]
    public async Task Estado_EnPermiso_NoEstaTrabajando_YDiceHastaCuando()
    {
        var s = await SeedAsync();
        var now = FixedTimeProvider.WorkingHours.GetUtcNow().UtcDateTime;
        using (var arrange = TonerTestDb.CreateContext(s.DbName))
        {
            arrange.Add(new TechnicianTimeOff { TechnicianId = s.TechnicianId, StartsAt = now.AddHours(-1), EndsAt = now.AddDays(2), CreatedByUserId = s.ReporterId });
            await arrange.SaveChangesAsync();
        }

        using var db = TonerTestDb.CreateContext(s.DbName);
        var home = await Service(db).GetAsync(s.TechnicianId);

        Assert.False(home.IsWorkingNow);
        Assert.Equal(now.AddDays(2), home.TimeOffUntil);
    }

    [Fact]
    public async Task StockBajo_SoloDeLasZonasDelTecnico_ConElNombreDeLaZona()
    {
        var s = await SeedAsync();
        using (var arrange = TonerTestDb.CreateContext(s.DbName))
        {
            var low = new InventoryItem { Name = "Fusor", Category = InventoryCategory.ConsumibleBase, MinimumStock = 3 };
            var fine = new InventoryItem { Name = "Cilindro", Category = InventoryCategory.ConsumibleBase, MinimumStock = 1 };
            var negative = new InventoryItem { Name = "Empaque", Category = InventoryCategory.Repuesto };
            var otherZone = new Zone { Name = "Norte" };
            var otherLocation = new InventoryLocation { Kind = InventoryLocationKind.Zona, Zone = otherZone };
            arrange.AddRange(low, fine, negative, otherZone, otherLocation);
            InventoryMovement M(InventoryItem item, InventoryLocation where, int delta) =>
                new() { InventoryItemId = item.Id, InventoryLocationId = where.Id, Type = InventoryMovementType.Ajuste, Delta = delta };
            var zoneLocation = await arrange.InventoryLocations.SingleAsync(l => l.Id == s.ZoneLocationId);
            arrange.InventoryMovements.AddRange(M(low, zoneLocation, 2), M(fine, zoneLocation, 9), M(negative, zoneLocation, -2), M(low, otherLocation, 0), M(fine, otherLocation, -4));
            await arrange.SaveChangesAsync();
        }

        using var db = TonerTestDb.CreateContext(s.DbName);
        var stock = (await Service(db).GetAsync(s.TechnicianId)).LowStock;

        Assert.Equal(new[] { "Empaque", "Fusor" }, stock.Select(x => x.ItemName));   // el negativo primero
        Assert.All(stock, x => Assert.Equal("Sur", x.LocationName));
    }

    [Fact]
    public async Task TecnicoInexistente_DaNotFound()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        await Assert.ThrowsAsync<Toner.Application.Common.Exceptions.NotFoundException>(() => Service(db).GetAsync(Guid.NewGuid()));
    }
}
