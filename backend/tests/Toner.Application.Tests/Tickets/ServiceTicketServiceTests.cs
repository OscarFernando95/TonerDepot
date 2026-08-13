using Toner.Application.Assignment;
using Toner.Application.Common.Exceptions;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Tickets;
using Toner.Application.Tickets.Dtos;
using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Tickets;

public class ServiceTicketServiceTests
{
    private static ServiceTicketService BuildService(Infrastructure.Persistence.TonerDbContext db) =>
        new(db, new AssignmentEngine(db));

    [Fact]
    public async Task SetStatusAsync_Abierto_ToAsignado_ThrowsBecauseOnlyAssignCanDoThat()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Abierto);
        arrangeDb.AddRange(role, user, city, client, location, ticket);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.SetStatusAsync(ticket.Id, nameof(ServiceTicketStatus.Asignado)));
    }

    [Fact]
    public async Task SetStatusAsync_ToResuelto_SetsResolvedAt()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.EnProceso);
        arrangeDb.AddRange(role, user, city, client, location, ticket);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var result = await service.SetStatusAsync(ticket.Id, nameof(ServiceTicketStatus.Resuelto));

        Assert.Equal(nameof(ServiceTicketStatus.Resuelto), result.Status);
        Assert.NotNull(result.ResolvedAt);
        Assert.Null(result.ClosedAt);
    }

    [Fact]
    public async Task SetStatusAsync_ResueltoBackToEnProceso_IsAllowed()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Resuelto);
        arrangeDb.AddRange(role, user, city, client, location, ticket);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var result = await service.SetStatusAsync(ticket.Id, nameof(ServiceTicketStatus.EnProceso));

        Assert.Equal(nameof(ServiceTicketStatus.EnProceso), result.Status);
    }

    [Fact]
    public async Task SetStatusAsync_Cerrado_IsTerminal()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Cerrado);
        arrangeDb.AddRange(role, user, city, client, location, ticket);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.SetStatusAsync(ticket.Id, nameof(ServiceTicketStatus.EnProceso)));
    }

    [Fact]
    public async Task AssignAsync_FromAbierto_SetsAsignadoAndCreatesHistory()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Abierto);
        arrangeDb.AddRange(role, user, techRole, techUser, technician, city, client, location, ticket);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);
        var assignedBy = Guid.NewGuid();

        var result = await service.AssignAsync(
            ticket.Id,
            new AssignTicketRequest { TechnicianId = technician.Id, Reason = "Manual" },
            assignedBy);

        Assert.Equal(nameof(ServiceTicketStatus.Asignado), result.Status);
        Assert.Equal(technician.Id, result.TechnicianId);

        var history = await service.GetAssignmentHistoryAsync(ticket.Id);
        var entry = Assert.Single(history);
        Assert.Equal("Manual", entry.Reason);
        Assert.Equal(nameof(AssignmentType.Manual), entry.AssignmentType);
    }

    [Theory]
    [InlineData(ServiceTicketStatus.EnProceso)]
    [InlineData(ServiceTicketStatus.Resuelto)]
    [InlineData(ServiceTicketStatus.Cerrado)]
    [InlineData(ServiceTicketStatus.Cancelado)]
    public async Task AssignAsync_FromNonAssignableStatus_Throws(ServiceTicketStatus status)
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var ticket = TestEntities.ServiceTicket(location, user, status);
        arrangeDb.AddRange(role, user, city, client, location, ticket);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await Assert.ThrowsAsync<ConflictException>(() => service.AssignAsync(
            ticket.Id,
            new AssignTicketRequest { TechnicianId = Guid.NewGuid() },
            Guid.NewGuid()));
    }
}
