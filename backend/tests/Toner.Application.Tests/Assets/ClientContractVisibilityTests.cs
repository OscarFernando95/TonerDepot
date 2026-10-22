using Toner.Application.Assets;
using Toner.Application.Common;
using Toner.Application.Maintenance;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Tickets;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Assets;

// El cliente no ve nada de contratos: ni su vigencia, ni su id, ni siquiera si un equipo está bajo uno.
public class ClientContractVisibilityTests
{
    private sealed record Seed(string DbName, Client Client, Guid ClientUserId, Asset Asset, ServiceTicket Ticket, Guid ContractId);

    private static async Task<Seed> SeedAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        var contract = TestEntities.Contract(client);
        var link = TestEntities.ContractAsset(contract, asset);
        var role = TestEntities.Role(RoleNames.Cliente);
        var reporter = TestEntities.User(role, clientId: client.Id);
        var ticket = TestEntities.ServiceTicket(location, reporter);
        ticket.ClientId = client.Id;
        ticket.AssetId = asset.Id;
        db.AddRange(city, brand, model, client, location, asset, contract, link, role, reporter, ticket);
        await db.SaveChangesAsync();
        return new Seed(dbName, client, reporter.Id, asset, ticket, contract.Id);
    }

    private static RequestingUser Client(Seed s) => new(s.ClientUserId, RoleNames.Cliente, s.Client.Id, null);
    private static RequestingUser Staff() => new(Guid.NewGuid(), RoleNames.Coordinador, null, null);

    private static AssetService Assets(Infrastructure.Persistence.TonerDbContext db) =>
        new(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));

    [Fact]
    public async Task Activos_ElStaffVeElContratoVigente_ElClienteNo()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = Assets(db);

        Assert.Equal(s.ContractId, (await service.GetByIdAsync(Staff(), s.Asset.Id)).ActiveContractId);
        Assert.Equal(s.ContractId, Assert.Single((await service.ListAsync(Staff(), null, null)).Items).ActiveContractId);

        Assert.Null((await service.GetByIdAsync(Client(s), s.Asset.Id)).ActiveContractId);
        Assert.Null(Assert.Single((await service.ListAsync(Client(s), null, null)).Items).ActiveContractId);
    }

    [Fact]
    public async Task Tickets_LaListaDelCliente_NuncaMarcaElEquipoComoBajoContrato()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = new ServiceTicketService(db, TestAssignment.Create(db));

        Assert.True(Assert.Single((await service.ListAsync(Staff(), null, null)).Items).AssetUnderContract);
        Assert.False(Assert.Single((await service.ListAsync(Client(s), null, null)).Items).AssetUnderContract);
    }
}
