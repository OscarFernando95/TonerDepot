using Microsoft.EntityFrameworkCore;
using Toner.Application.Assignment;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Assignment;

public class AssignmentEngineTests
{
    [Fact]
    public async Task AssignServiceTicketAsync_NoCandidatesInCity_SetsSinAsignarAndLogsReason()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var otherCity = TestEntities.City("Medellín");
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var ticket = TestEntities.ServiceTicket(location, user);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser);
        var coverage = TestEntities.Coverage(technician, otherCity); // cobertura en otra ciudad, no en la del ticket

        arrangeDb.AddRange(role, user, city, otherCity, client, location, ticket, techRole, techUser, technician, coverage);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new AssignmentEngine(actDb);

        // El motor ya no guarda: recibe la entidad trackeada y el caller decide cuándo persistir.
        var tracked = await actDb.ServiceTickets.SingleAsync(t => t.Id == ticket.Id);
        var result = await engine.AssignServiceTicketAsync(tracked);
        await actDb.SaveChangesAsync();

        Assert.Null(result);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedTicket = await assertDb.ServiceTickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(ServiceTicketStatus.SinAsignar, updatedTicket.Status);

        var history = await assertDb.AssignmentHistories.SingleAsync(h => h.ServiceTicketId == ticket.Id);
        Assert.Null(history.TechnicianId);
        Assert.Contains("cobertura", history.Reason);
    }

    [Fact]
    public async Task AssignServiceTicketAsync_ExcludesOcupadoTechnicians()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var ticket = TestEntities.ServiceTicket(location, user);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var busyUser = TestEntities.User(techRole);
        var busyTechnician = TestEntities.Technician(busyUser, isActive: true, status: TechnicianStatus.Ocupado);
        var busyCoverage = TestEntities.Coverage(busyTechnician, city);

        arrangeDb.AddRange(role, user, city, client, location, ticket, techRole, busyUser, busyTechnician, busyCoverage);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new AssignmentEngine(actDb);

        var tracked = await actDb.ServiceTickets.SingleAsync(t => t.Id == ticket.Id);
        var result = await engine.AssignServiceTicketAsync(tracked);

        Assert.Null(result);
    }

    [Fact]
    public async Task AssignServiceTicketAsync_PicksLeastLoadedCandidate()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);

        var techRole = TestEntities.Role(RoleNames.Tecnico);

        var busyUser = TestEntities.User(techRole);
        var busyTechnician = TestEntities.Technician(busyUser, isActive: true, status: TechnicianStatus.Disponible);
        var busyCoverage = TestEntities.Coverage(busyTechnician, city);
        // Este técnico ya tiene un ticket Asignado -> carga 1.
        var existingTicket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Asignado, technicianId: busyTechnician.Id);

        var freeUser = TestEntities.User(techRole);
        var freeTechnician = TestEntities.Technician(freeUser, isActive: true, status: TechnicianStatus.Disponible);
        var freeCoverage = TestEntities.Coverage(freeTechnician, city);
        // Carga 0: debe ganar la asignación.

        var newTicket = TestEntities.ServiceTicket(location, user);

        arrangeDb.AddRange(
            role, user, city, client, location, techRole,
            busyUser, busyTechnician, busyCoverage, existingTicket,
            freeUser, freeTechnician, freeCoverage,
            newTicket);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new AssignmentEngine(actDb);

        var tracked = await actDb.ServiceTickets.SingleAsync(t => t.Id == newTicket.Id);
        var result = await engine.AssignServiceTicketAsync(tracked);

        Assert.Equal(freeTechnician.Id, result);
    }

    [Fact]
    public async Task AssignMaintenanceOrderAsync_AssetWithoutLocation_SetsNoTechnicianWithReason()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.EnBodega); // sin sede actual
        var schedule = TestEntities.MaintenanceSchedule(asset, contract);
        var order = TestEntities.MaintenanceOrder(schedule, asset);

        arrangeDb.AddRange(client, contract, brand, model, asset, schedule, order);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new AssignmentEngine(actDb);

        var tracked = await actDb.MaintenanceOrders.SingleAsync(o => o.Id == order.Id);
        var result = await engine.AssignMaintenanceOrderAsync(tracked);
        await actDb.SaveChangesAsync();

        Assert.Null(result);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var history = await assertDb.AssignmentHistories.SingleAsync(h => h.MaintenanceOrderId == order.Id);
        Assert.Contains("sede de instalación", history.Reason);
    }

    [Fact]
    public async Task AssignMaintenanceOrderAsync_UsesAssetCurrentLocationCity_AssignsCandidate()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        var schedule = TestEntities.MaintenanceSchedule(asset, contract);
        var order = TestEntities.MaintenanceOrder(schedule, asset);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);
        var coverage = TestEntities.Coverage(technician, city);

        arrangeDb.AddRange(city, client, location, contract, brand, model, asset, schedule, order, techRole, techUser, technician, coverage);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var engine = new AssignmentEngine(actDb);

        var tracked = await actDb.MaintenanceOrders.SingleAsync(o => o.Id == order.Id);
        var result = await engine.AssignMaintenanceOrderAsync(tracked);
        await actDb.SaveChangesAsync();

        Assert.Equal(technician.Id, result);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedOrder = await assertDb.MaintenanceOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(MaintenanceOrderStatus.Asignada, updatedOrder.Status);
    }
}
