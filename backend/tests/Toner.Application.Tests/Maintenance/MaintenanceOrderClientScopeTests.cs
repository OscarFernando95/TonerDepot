using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Maintenance;
using Toner.Application.Maintenance.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Maintenance;

// El cliente sigue sus mantenimientos programados: solo los de su empresa, de solo lectura, sin las notas internas.
public class MaintenanceOrderClientScopeTests
{
    private sealed record Seed(string DbName, Client Mine, Guid MyUserId, Client Other, MaintenanceOrder MyOrder, MaintenanceOrder OtherOrder);

    private static async Task<Seed> SeedAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var mine = TestEntities.Client("Mi empresa");
        var other = TestEntities.Client("Otra empresa");
        var myLocation = TestEntities.ClientLocation(mine, city, "Sede mía");
        var otherLocation = TestEntities.ClientLocation(other, city, "Sede ajena");
        var myAsset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, myLocation.Id);
        var otherAsset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, otherLocation.Id);
        var role = TestEntities.Role(RoleNames.Cliente);
        var myUser = TestEntities.User(role, clientId: mine.Id);
        var contract = new Contract { Id = Guid.NewGuid() }; // solo la referencia del cronograma; no se persiste
        var mySchedule = TestEntities.MaintenanceSchedule(myAsset, contract);
        var otherSchedule = TestEntities.MaintenanceSchedule(otherAsset, contract);
        var myOrder = TestEntities.MaintenanceOrder(mySchedule, myAsset, MaintenanceOrderStatus.Asignada);
        myOrder.ClientId = mine.Id;
        myOrder.Reason = "Cliente molesto: prioridad interna";
        myOrder.IsManual = true;
        var otherOrder = TestEntities.MaintenanceOrder(otherSchedule, otherAsset, MaintenanceOrderStatus.Asignada);
        otherOrder.ClientId = other.Id;
        db.AddRange(role, myUser, city, brand, model, mine, other, myLocation, otherLocation, myAsset, otherAsset,
            mySchedule, otherSchedule, myOrder, otherOrder);
        await db.SaveChangesAsync();
        return new Seed(dbName, mine, myUser.Id, other, myOrder, otherOrder);
    }

    private static RequestingUser Client(Seed s) => new(s.MyUserId, RoleNames.Cliente, s.Mine.Id, null);
    private static RequestingUser ClientWithoutCompany() => new(Guid.NewGuid(), RoleNames.Cliente, null, null);

    private static MaintenanceOrderService Service(Infrastructure.Persistence.TonerDbContext db) =>
        new(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));

    [Fact]
    public async Task List_ElClienteVeSoloLasOrdenesDeSuEmpresa_SinElMotivoInterno()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        var page = await Service(db).ListAsync(Client(s), null, null);

        var order = Assert.Single(page.Items);
        Assert.Equal(s.MyOrder.Id, order.Id);
        Assert.Null(order.Reason);
    }

    [Fact]
    public async Task List_ElClienteNoAmpliaSuAlcancePasandoElClientIdDeOtro()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        var page = await Service(db).ListAsync(Client(s), null, null, new MaintenanceOrderListFilter(null, s.Other.Id, null));

        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task List_ElStaffSigueViendoTodoConElMotivo()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        var page = await Service(db).ListAsync(new RequestingUser(Guid.NewGuid(), RoleNames.Coordinador, null, null), null, null);

        Assert.Equal(2, page.Items.Count);
        Assert.Contains(page.Items, o => o.Reason == "Cliente molesto: prioridad interna");
    }

    [Fact]
    public async Task GetById_LaOrdenPropia_SinElMotivoInterno()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        var order = await Service(db).GetByIdAsync(Client(s), s.MyOrder.Id);

        Assert.Equal(s.MyOrder.Id, order.Id);
        Assert.Null(order.Reason);
    }

    [Fact]
    public async Task GetById_LaOrdenDeOtraEmpresa_EsForbidden()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        await Assert.ThrowsAsync<ForbiddenException>(() => Service(db).GetByIdAsync(Client(s), s.OtherOrder.Id));
    }

    [Fact]
    public async Task ClienteSinClientIdAsociado_SeDeniega()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        await Assert.ThrowsAsync<ForbiddenException>(() => Service(db).ListAsync(ClientWithoutCompany(), null, null));
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(db).GetByIdAsync(ClientWithoutCompany(), s.MyOrder.Id));
    }
}
