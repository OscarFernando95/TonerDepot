using Microsoft.EntityFrameworkCore;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Maintenance;
using Toner.Application.Maintenance.Dtos;
using Toner.Application.Maintenance.Validators;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Maintenance;

// El técnico programa el mantenimiento y el cliente, desde su usuario, ve la fecha y deja comentarios si no le sirve.
public class MaintenanceOrderThreadTests
{
    private sealed record World(
        string DbName, Client Client, Client OtherClient, ClientLocation Norte, ClientLocation Sur,
        Technician Tech, User TechUser, Technician OtherTech, MaintenanceOrder Order, MaintenanceOrder OrderSur,
        User ClientUser, User OtherClientUser, User StaffUser);

    private static async Task<World> SeedAsync(MaintenanceOrderStatus status = MaintenanceOrderStatus.Asignada)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var client = TestEntities.Client("Mi empresa");
        var other = TestEntities.Client("Otra empresa");
        var norte = TestEntities.ClientLocation(client, city, "Norte");
        var sur = TestEntities.ClientLocation(client, city, "Sur");
        var otherLoc = TestEntities.ClientLocation(other, city, "Ajena");
        var assetN = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, norte.Id);
        var assetS = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, sur.Id);
        var clientRole = TestEntities.Role(RoleNames.Cliente);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var adminRole = TestEntities.Role(RoleNames.Coordinador);
        var clientUser = TestEntities.User(clientRole, clientId: client.Id);
        var otherClientUser = TestEntities.User(clientRole, clientId: other.Id);
        var techUser = TestEntities.User(techRole);
        var otherTechUser = TestEntities.User(techRole);
        var staffUser = TestEntities.User(adminRole);
        var tech = TestEntities.Technician(techUser);
        var otherTech = TestEntities.Technician(otherTechUser);
        var contract = new Contract { Id = Guid.NewGuid() };
        var scheduleN = TestEntities.MaintenanceSchedule(assetN, contract);
        var scheduleS = TestEntities.MaintenanceSchedule(assetS, contract);
        var order = TestEntities.MaintenanceOrder(scheduleN, assetN, status, technicianId: tech.Id);
        order.ClientId = client.Id;
        var orderSur = TestEntities.MaintenanceOrder(scheduleS, assetS, status, technicianId: tech.Id);
        orderSur.ClientId = client.Id;
        db.AddRange(city, brand, model, client, other, norte, sur, otherLoc, assetN, assetS, clientRole, techRole, adminRole,
            clientUser, otherClientUser, techUser, otherTechUser, staffUser, tech, otherTech, scheduleN, scheduleS, order, orderSur);
        await db.SaveChangesAsync();
        return new World(dbName, client, other, norte, sur, tech, techUser, otherTech, order, orderSur, clientUser, otherClientUser, staffUser);
    }

    private static MaintenanceOrderService Service(Infrastructure.Persistence.TonerDbContext db) =>
        new(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));

    private static RequestingUser AsClient(World w) => new(w.ClientUser.Id, RoleNames.Cliente, w.Client.Id, null);
    private static RequestingUser AsTech(World w) => new(w.TechUser.Id, RoleNames.Tecnico, null, w.Tech.Id);
    private static RequestingUser AsStaff(World w) => new(w.StaffUser.Id, RoleNames.Coordinador, null, null);

    private static DateTime InAWeek() => DateTime.UtcNow.AddDays(7);

    [Fact]
    public async Task Programar_ElTecnicoAsignadoFijaLaFecha_YQuedaEnElHilo()
    {
        var w = await SeedAsync();
        var when = InAWeek();
        using var db = TonerTestDb.CreateContext(w.DbName);

        var dto = await Service(db).ScheduleAsync(w.Order.Id, w.Tech.Id, w.TechUser.Id, when);

        Assert.Equal(when, dto.PlannedAt!.Value, TimeSpan.FromSeconds(1));
        Assert.NotNull(dto.LastCommentAt);
        var entry = await db.MaintenanceOrderComments.SingleAsync();
        Assert.Equal(OrderCommentKind.Programacion, entry.Kind);
        Assert.Equal(RoleNames.Tecnico, entry.AuthorRole);
        Assert.Equal(w.Client.Id, entry.ClientId); // capturado de la orden, para la política RLS
        Assert.Equal(when, entry.PlannedAt!.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Programar_OtroTecnico_OUnaOrdenYaIniciada_SeRechaza()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);
        var service = Service(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ScheduleAsync(w.Order.Id, w.OtherTech.Id, Guid.NewGuid(), InAWeek()));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ScheduleAsync(Guid.NewGuid(), w.Tech.Id, w.TechUser.Id, InAWeek()));

        var started = await SeedAsync(MaintenanceOrderStatus.EnProceso);
        using var db2 = TonerTestDb.CreateContext(started.DbName);
        await Assert.ThrowsAsync<ConflictException>(() => Service(db2).ScheduleAsync(started.Order.Id, started.Tech.Id, started.TechUser.Id, InAWeek()));
        Assert.Empty(db.MaintenanceOrderComments);
    }

    [Fact]
    public async Task Reprogramar_CambiaLaFecha_YDejaLaHistoriaCompleta()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);
        var service = Service(db);
        await service.ScheduleAsync(w.Order.Id, w.Tech.Id, w.TechUser.Id, InAWeek());
        var later = DateTime.UtcNow.AddDays(10);

        var dto = await service.ScheduleAsync(w.Order.Id, w.Tech.Id, w.TechUser.Id, later);

        Assert.Equal(later, dto.PlannedAt!.Value, TimeSpan.FromSeconds(1));
        Assert.Equal(2, await db.MaintenanceOrderComments.CountAsync(c => c.Kind == OrderCommentKind.Programacion));
    }

    [Fact]
    public async Task ReasignarLaOrden_DescartaLaFechaDelTecnicoAnterior()
    {
        var w = await SeedAsync();
        using (var db = TonerTestDb.CreateContext(w.DbName))
        {
            await Service(db).ScheduleAsync(w.Order.Id, w.Tech.Id, w.TechUser.Id, InAWeek());
        }

        using (var db = TonerTestDb.CreateContext(w.DbName))
        {
            var dto = await Service(db).AssignAsync(w.Order.Id, new AssignMaintenanceOrderRequest { TechnicianId = w.OtherTech.Id }, w.StaffUser.Id);
            Assert.Null(dto.PlannedAt);
        }
    }

    [Fact]
    public async Task ElClienteVeLaFechaProgramada_YElConteoDeComentarios()
    {
        var w = await SeedAsync();
        using (var db = TonerTestDb.CreateContext(w.DbName))
        {
            await Service(db).ScheduleAsync(w.Order.Id, w.Tech.Id, w.TechUser.Id, InAWeek());
        }

        using var read = TonerTestDb.CreateContext(w.DbName);
        var seen = await Service(read).GetByIdAsync(AsClient(w), w.Order.Id);

        Assert.NotNull(seen.PlannedAt);
        Assert.Equal(1, seen.CommentCount);
    }

    [Fact]
    public async Task Comentar_ElClientePidePosponer_ElTecnicoLoVe_YLaOrdenQuedaMarcada()
    {
        var w = await SeedAsync();
        using (var db = TonerTestDb.CreateContext(w.DbName))
        {
            await Service(db).ScheduleAsync(w.Order.Id, w.Tech.Id, w.TechUser.Id, InAWeek());
        }

        using var db2 = TonerTestDb.CreateContext(w.DbName);
        var service = Service(db2);
        var added = await service.AddCommentAsync(AsClient(w), w.Order.Id, "  Ese día no estamos, ¿lo pasamos al jueves?  ");

        Assert.Equal("Ese día no estamos, ¿lo pasamos al jueves?", added.Body); // recortado
        Assert.Equal(RoleNames.Cliente, added.AuthorRole);
        Assert.Equal(w.ClientUser.FullName, added.AuthorName);

        var thread = await service.ListCommentsAsync(AsTech(w), w.Order.Id);
        Assert.Equal(new[] { "Programacion", "Comentario" }, thread.Select(c => c.Kind));
        Assert.Equal(RoleNames.Cliente, thread[1].AuthorRole);
        Assert.NotNull((await db2.MaintenanceOrders.SingleAsync(o => o.Id == w.Order.Id)).LastCommentAt);
    }

    [Fact]
    public async Task Comentar_ElStaffYElTecnicoAsignadoTambienPueden()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);
        var service = Service(db);

        await service.AddCommentAsync(AsTech(w), w.Order.Id, "Paso el jueves a las 10:00.");
        await service.AddCommentAsync(AsStaff(w), w.Order.Id, "Confirmado con el cliente.");

        var roles = (await service.ListCommentsAsync(AsStaff(w), w.Order.Id)).Select(c => c.AuthorRole);
        Assert.Equal(new[] { RoleNames.Tecnico, RoleNames.Coordinador }, roles);
    }

    [Fact]
    public async Task NadieAjeno_VeNiEscribe_EnElHilo()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);
        var service = Service(db);
        var otherClient = new RequestingUser(w.OtherClientUser.Id, RoleNames.Cliente, w.OtherClient.Id, null);
        var otherTech = new RequestingUser(Guid.NewGuid(), RoleNames.Tecnico, null, w.OtherTech.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ListCommentsAsync(otherClient, w.Order.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.AddCommentAsync(otherClient, w.Order.Id, "intruso"));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ListCommentsAsync(otherTech, w.Order.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.AddCommentAsync(otherTech, w.Order.Id, "intruso"));
        Assert.Empty(db.MaintenanceOrderComments);
    }

    [Fact]
    public async Task ElClienteRestringidoASedes_SoloUsaElHiloDeLasOrdenesDeSuSede()
    {
        var w = await SeedAsync();
        var restricted = await TestClientUsers.SeedAsync(w.DbName, w.Client.Id, restrictedToLocations: true, w.Norte.Id);
        using var db = TonerTestDb.CreateContext(w.DbName);
        var service = Service(db);

        await service.AddCommentAsync(restricted, w.Order.Id, "En mi sede, sí");
        await Assert.ThrowsAsync<ForbiddenException>(() => service.AddCommentAsync(restricted, w.OrderSur.Id, "En la otra sede, no"));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ListCommentsAsync(restricted, w.OrderSur.Id));
    }

    [Theory]
    [InlineData(MaintenanceOrderStatus.Completada)]
    [InlineData(MaintenanceOrderStatus.Cancelada)]
    public async Task UnaOrdenCerrada_NoAdmiteMasComentarios(MaintenanceOrderStatus status)
    {
        var w = await SeedAsync(status);
        using var db = TonerTestDb.CreateContext(w.DbName);

        await Assert.ThrowsAsync<ConflictException>(() => Service(db).AddCommentAsync(AsClient(w), w.Order.Id, "tarde"));
    }

    [Fact]
    public void Validadores_ComentarioNoVacioNiLargo_FechaFuturaYAcotada()
    {
        var comment = new AddOrderCommentRequestValidator();
        Assert.True(comment.Validate(new AddOrderCommentRequest { Body = "hola" }).IsValid);
        Assert.False(comment.Validate(new AddOrderCommentRequest { Body = "   " }).IsValid);
        Assert.False(comment.Validate(new AddOrderCommentRequest { Body = new string('x', AddOrderCommentRequestValidator.MaxLength + 1) }).IsValid);

        var schedule = new ScheduleMaintenanceOrderRequestValidator();
        Assert.True(schedule.Validate(new ScheduleMaintenanceOrderRequest { PlannedAt = InAWeek() }).IsValid);
        Assert.False(schedule.Validate(new ScheduleMaintenanceOrderRequest { PlannedAt = DateTime.UtcNow.AddMinutes(-1) }).IsValid);
        Assert.False(schedule.Validate(new ScheduleMaintenanceOrderRequest { PlannedAt = DateTime.UtcNow.AddDays(400) }).IsValid);
    }
}
