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
    private sealed record Seed(string DbName, Client Mine, Client Other, MaintenanceOrder MyOrder, MaintenanceOrder OtherOrder);

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
        var contract = new Contract { Id = Guid.NewGuid() }; // solo la referencia del cronograma; no se persiste
        var mySchedule = TestEntities.MaintenanceSchedule(myAsset, contract);
        var otherSchedule = TestEntities.MaintenanceSchedule(otherAsset, contract);
        var myOrder = TestEntities.MaintenanceOrder(mySchedule, myAsset, MaintenanceOrderStatus.Asignada);
        myOrder.ClientId = mine.Id;
        myOrder.Reason = "Cliente molesto: prioridad interna";
        myOrder.IsManual = true;
        var otherOrder = TestEntities.MaintenanceOrder(otherSchedule, otherAsset, MaintenanceOrderStatus.Asignada);
        otherOrder.ClientId = other.Id;
        db.AddRange(city, brand, model, mine, other, myLocation, otherLocation, myAsset, otherAsset,
            mySchedule, otherSchedule, myOrder, otherOrder);
        await db.SaveChangesAsync();
        return new Seed(dbName, mine, other, myOrder, otherOrder);
    }

    private static RequestingUser Client(Guid? clientId) => new(Guid.NewGuid(), RoleNames.Cliente, clientId, null);

    private static MaintenanceOrderService Service(Infrastructure.Persistence.TonerDbContext db) =>
        new(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));

    [Fact]
    public async Task List_ElClienteVeSoloLasOrdenesDeSuEmpresa_SinElMotivoInterno()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        var page = await Service(db).ListAsync(Client(s.Mine.Id), null, null);

        var order = Assert.Single(page.Items);
        Assert.Equal(s.MyOrder.Id, order.Id);
        Assert.Null(order.Reason);
    }

    [Fact]
    public async Task List_ElClienteNoAmpliaSuAlcancePasandoElClientIdDeOtro()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        var page = await Service(db).ListAsync(Client(s.Mine.Id), null, null, new MaintenanceOrderListFilter(null, s.Other.Id, null));

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

        var order = await Service(db).GetByIdAsync(Client(s.Mine.Id), s.MyOrder.Id);

        Assert.Equal(s.MyOrder.Id, order.Id);
        Assert.Null(order.Reason);
    }

    [Fact]
    public async Task GetById_LaOrdenDeOtraEmpresa_EsForbidden()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        await Assert.ThrowsAsync<ForbiddenException>(() => Service(db).GetByIdAsync(Client(s.Mine.Id), s.OtherOrder.Id));
    }

    [Fact]
    public async Task ClienteSinClientIdAsociado_SeDeniega()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        await Assert.ThrowsAsync<ForbiddenException>(() => Service(db).ListAsync(Client(null), null, null));
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(db).GetByIdAsync(Client(null), s.MyOrder.Id));
    }
}
