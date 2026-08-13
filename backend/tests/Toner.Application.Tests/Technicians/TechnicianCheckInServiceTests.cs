using Microsoft.EntityFrameworkCore;
using Toner.Application.Assignment;
using Toner.Application.Common.Exceptions;
using Toner.Application.Maintenance;
using Toner.Application.Technicians;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Tickets;
using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Technicians;

public class TechnicianCheckInServiceTests
{
    private static TechnicianCheckInService BuildService(Infrastructure.Persistence.TonerDbContext db) =>
        new(db, new ServiceTicketService(db, new AssignmentEngine(db)), new MaintenanceOrderService(db));

    [Fact]
    public async Task CheckInAsync_TicketAssignedToTechnician_MovesTicketAndTechnicianToInProgress()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);

        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Asignado, technicianId: technician.Id);

        arrangeDb.AddRange(role, user, city, client, location, techRole, techUser, technician, ticket);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var status = await service.CheckInAsync(technician.Id, new CheckInRequest { ServiceTicketId = ticket.Id });

        Assert.Equal(nameof(TechnicianStatus.Ocupado), status.Status);
        Assert.Equal(ticket.Id, status.ActiveServiceTicketId);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedTicket = await assertDb.ServiceTickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(ServiceTicketStatus.EnProceso, updatedTicket.Status);
        var updatedTechnician = await assertDb.Technicians.SingleAsync(t => t.Id == technician.Id);
        Assert.Equal(TechnicianStatus.Ocupado, updatedTechnician.Status);
    }

    [Fact]
    public async Task CheckInAsync_TechnicianAlreadyOcupado_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Ocupado);

        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Asignado, technicianId: technician.Id);

        arrangeDb.AddRange(role, user, city, client, location, techRole, techUser, technician, ticket);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CheckInAsync(technician.Id, new CheckInRequest { ServiceTicketId = ticket.Id }));
    }

    [Fact]
    public async Task CheckInAsync_TicketAssignedToAnotherTechnician_ThrowsForbidden()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var ownerUser = TestEntities.User(techRole);
        var ownerTechnician = TestEntities.Technician(ownerUser);
        var otherUser = TestEntities.User(techRole);
        var otherTechnician = TestEntities.Technician(otherUser);

        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Asignado, technicianId: ownerTechnician.Id);

        arrangeDb.AddRange(role, user, city, client, location, techRole, ownerUser, ownerTechnician, otherUser, otherTechnician, ticket);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CheckInAsync(otherTechnician.Id, new CheckInRequest { ServiceTicketId = ticket.Id }));
    }

    [Fact]
    public async Task CheckOutAsync_Resolved_MarksTicketResueltoAndFreesUpTechnician()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);
        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Asignado, technicianId: technician.Id);

        arrangeDb.AddRange(role, user, city, client, location, techRole, techUser, technician, ticket);
        await arrangeDb.SaveChangesAsync();

        using (var checkInDb = TonerTestDb.CreateContext(dbName))
        {
            var checkInService = BuildService(checkInDb);
            await checkInService.CheckInAsync(technician.Id, new CheckInRequest { ServiceTicketId = ticket.Id });
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var status = await service.CheckOutAsync(technician.Id, new CheckOutRequest { Resolved = true });

        Assert.Equal(nameof(TechnicianStatus.Disponible), status.Status);
        Assert.Null(status.ActiveServiceTicketId);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedTicket = await assertDb.ServiceTickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(ServiceTicketStatus.Resuelto, updatedTicket.Status);
        Assert.NotNull(updatedTicket.ResolvedAt);
    }

    [Fact]
    public async Task CheckOutAsync_NotResolved_LeavesTicketInProgressButFreesUpTechnician()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);
        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Asignado, technicianId: technician.Id);

        arrangeDb.AddRange(role, user, city, client, location, techRole, techUser, technician, ticket);
        await arrangeDb.SaveChangesAsync();

        using (var checkInDb = TonerTestDb.CreateContext(dbName))
        {
            var checkInService = BuildService(checkInDb);
            await checkInService.CheckInAsync(technician.Id, new CheckInRequest { ServiceTicketId = ticket.Id });
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var status = await service.CheckOutAsync(technician.Id, new CheckOutRequest { Resolved = false, Notes = "Falta repuesto" });

        Assert.Equal(nameof(TechnicianStatus.Disponible), status.Status);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedTicket = await assertDb.ServiceTickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(ServiceTicketStatus.EnProceso, updatedTicket.Status);
        Assert.Null(updatedTicket.ResolvedAt);
    }

    [Fact]
    public async Task CheckOutAsync_MaintenanceOrder_Resolved_CompletesOrderAndRecalculatesSchedule()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var asset = TestEntities.Asset(brand);
        var schedule = TestEntities.MaintenanceSchedule(asset, MaintenanceFrequencyType.PorTiempo, timeIntervalDays: 90);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);

        var order = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Asignada, technician.Id);

        arrangeDb.AddRange(brand, asset, schedule, techRole, techUser, technician, order);
        await arrangeDb.SaveChangesAsync();

        using (var checkInDb = TonerTestDb.CreateContext(dbName))
        {
            var checkInService = BuildService(checkInDb);
            await checkInService.CheckInAsync(technician.Id, new CheckInRequest { MaintenanceOrderId = order.Id });
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await service.CheckOutAsync(technician.Id, new CheckOutRequest { Resolved = true });

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedOrder = await assertDb.MaintenanceOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(MaintenanceOrderStatus.Completada, updatedOrder.Status);
        var updatedSchedule = await assertDb.MaintenanceSchedules.SingleAsync(s => s.Id == schedule.Id);
        Assert.NotNull(updatedSchedule.NextDueAt);
        Assert.NotNull(updatedSchedule.LastExecutedAt);
    }
}
