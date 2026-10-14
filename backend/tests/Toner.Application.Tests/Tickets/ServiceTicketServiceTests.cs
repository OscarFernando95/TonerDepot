using Toner.Domain.Entities;
using Toner.Application.Assignment;
using Toner.Application.Common;
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
        new(db, TestAssignment.Create(db));

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

        var history = (await service.GetAssignmentHistoryAsync(ticket.Id, null, null)).Items;
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

    [Fact]
    public async Task ClaimAsync_AssignedToOtherTechnicianButNotStarted_ReassignsAndCreatesHistory()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var ownerUser = TestEntities.User(techRole);
        var owner = TestEntities.Technician(ownerUser);
        var claimingUser = TestEntities.User(techRole);
        var claimingTechnician = TestEntities.Technician(claimingUser);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Asignado, technicianId: owner.Id);
        arrangeDb.AddRange(role, user, techRole, ownerUser, owner, claimingUser, claimingTechnician, city, client, location, ticket);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var result = await service.ClaimAsync(ticket.Id, claimingTechnician.Id);

        Assert.Equal(nameof(ServiceTicketStatus.Asignado), result.Status);
        Assert.Equal(claimingTechnician.Id, result.TechnicianId);

        var history = (await service.GetAssignmentHistoryAsync(ticket.Id, null, null)).Items;
        var entry = Assert.Single(history);
        Assert.Equal(nameof(AssignmentType.Reclamada), entry.AssignmentType);
    }

    [Fact]
    public async Task ClaimAsync_AlreadyEnProceso_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var ownerUser = TestEntities.User(techRole);
        var owner = TestEntities.Technician(ownerUser);
        var claimingUser = TestEntities.User(techRole);
        var claimingTechnician = TestEntities.Technician(claimingUser);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.EnProceso, technicianId: owner.Id);
        arrangeDb.AddRange(role, user, techRole, ownerUser, owner, claimingUser, claimingTechnician, city, client, location, ticket);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await Assert.ThrowsAsync<ConflictException>(() => service.ClaimAsync(ticket.Id, claimingTechnician.Id));
    }

    [Fact]
    public async Task ListInCoverageAsync_ExcludesOwnTicketsAndOtherCities()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser);
        var coveredCity = TestEntities.City("Medellín", "Antioquia");
        var otherCity = TestEntities.City("Cali", "Valle del Cauca");
        var coverage = TestEntities.Coverage(technician, coveredCity);
        var client = TestEntities.Client();
        var coveredLocation = TestEntities.ClientLocation(client, coveredCity);
        var otherLocation = TestEntities.ClientLocation(client, otherCity);

        var ticketInCoverage = TestEntities.ServiceTicket(coveredLocation, user, ServiceTicketStatus.Abierto);
        var ownTicketInCoverage = TestEntities.ServiceTicket(coveredLocation, user, ServiceTicketStatus.Asignado, technicianId: technician.Id);
        var ticketInOtherCity = TestEntities.ServiceTicket(otherLocation, user, ServiceTicketStatus.Abierto);

        arrangeDb.AddRange(
            role, user, techRole, techUser, technician, coveredCity, otherCity, coverage,
            client, coveredLocation, otherLocation, ticketInCoverage, ownTicketInCoverage, ticketInOtherCity);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var result = (await service.ListInCoverageAsync(technician.Id, null, null)).Items;

        var item = Assert.Single(result);
        Assert.Equal(ticketInCoverage.Id, item.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ClienteConClientIdNulo_Deniega()
    {
        // SECURITY_AUDIT.md hallazgo #20: un ClientId nulo debe denegar explícitamente.
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
        var malformedClientUser = new RequestingUser(Guid.NewGuid(), RoleNames.Cliente, null, null);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetByIdAsync(malformedClientUser, ticket.Id));
    }

    [Fact]
    public async Task ListAsync_ClienteConClientIdNulo_Deniega()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var service = BuildService(db);
        var malformedClientUser = new RequestingUser(Guid.NewGuid(), RoleNames.Cliente, null, null);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ListAsync(malformedClientUser, null, null));
    }

    // CODE_QUALITY_AUDIT.md hallazgo #20: Priority/Status inválidos deben mapear a 400
    // (ValidationException), no explotar como 500.
    [Fact]
    public async Task CreateAsync_InvalidPriority_ThrowsValidationException()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var service = BuildService(db);
        var staffUser = new RequestingUser(Guid.NewGuid(), RoleNames.Administrador, null, null);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => service.CreateAsync(
            staffUser,
            new CreateServiceTicketRequest { ClientLocationId = Guid.NewGuid(), Description = "Impresora atascada", Priority = "NoExiste" }));
    }

    [Fact]
    public async Task SetStatusAsync_InvalidStatus_ThrowsValidationException()
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

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.SetStatusAsync(ticket.Id, "NoExiste"));
    }

    // ── AUDIT.md H-11 (Paso 3): filtros en el servidor, antes de paginar ─────────────────────────────

    private sealed record ListSeed(string DbName, Guid ClientA, Guid ClientB, Guid StaffUserId);

    // Cliente A: 3 tickets (Abierto/Alta, Abierto/Media, Cerrado/Media). Cliente B: 1 ticket Abierto/Alta.
    private static async Task<ListSeed> SeedForListAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var clientA = TestEntities.Client("Cliente A");
        var clientB = TestEntities.Client("Cliente B");
        var locA = TestEntities.ClientLocation(clientA, city, "Sede A");
        var locB = TestEntities.ClientLocation(clientB, city, "Sede B");

        ServiceTicket T(ClientLocation l, Client c, ServiceTicketStatus st, ServiceTicketPriority pr, string desc)
        {
            var t = TestEntities.ServiceTicket(l, user, st, pr);
            t.ClientId = c.Id;
            t.Description = desc;
            return t;
        }

        db.AddRange(role, user, city, clientA, clientB, locA, locB,
            T(locA, clientA, ServiceTicketStatus.Abierto, ServiceTicketPriority.Alta, "Impresora sin tóner"),
            T(locA, clientA, ServiceTicketStatus.Abierto, ServiceTicketPriority.Media, "Atasco de papel"),
            T(locA, clientA, ServiceTicketStatus.Cerrado, ServiceTicketPriority.Media, "Cambio de fusor"),
            T(locB, clientB, ServiceTicketStatus.Abierto, ServiceTicketPriority.Alta, "Pantalla en negro"));
        await db.SaveChangesAsync();
        return new ListSeed(dbName, clientA.Id, clientB.Id, user.Id);
    }

    private static RequestingUser Staff(Guid id) => new(id, RoleNames.Coordinador, null, null);

    [Fact]
    public async Task ListAsync_FiltraPorEstadoPrioridadYCliente_EnElServidor()
    {
        var s = await SeedForListAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = BuildService(db);

        var byStatus = await service.ListAsync(Staff(s.StaffUserId), null, null, new ServiceTicketListFilter(Status: "Cerrado"));
        Assert.Equal(new[] { "Cambio de fusor" }, byStatus.Items.Select(t => t.Description));
        Assert.Equal(1, byStatus.TotalCount);

        var byPriorityAndClient = await service.ListAsync(Staff(s.StaffUserId), null, null,
            new ServiceTicketListFilter(Priority: "Alta", ClientId: s.ClientA));
        Assert.Equal(new[] { "Impresora sin tóner" }, byPriorityAndClient.Items.Select(t => t.Description));
    }

    [Fact]
    public async Task ListAsync_BuscaPorTextoSinDistinguirMayusculas_YElTotalEsElDelFiltro()
    {
        var s = await SeedForListAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = BuildService(db);

        var found = await service.ListAsync(Staff(s.StaffUserId), null, null, new ServiceTicketListFilter(Search: "PAPEL"));

        Assert.Equal(new[] { "Atasco de papel" }, found.Items.Select(t => t.Description));
        Assert.Equal(1, found.TotalCount);
    }

    [Fact]
    public async Task ListAsync_UnClienteNoVeMasPorPasarElClientIdDeOtro()
    {
        var s = await SeedForListAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = BuildService(db);
        var asClientA = new RequestingUser(Guid.NewGuid(), RoleNames.Cliente, s.ClientA, null);

        var other = await service.ListAsync(asClientA, null, null, new ServiceTicketListFilter(ClientId: s.ClientB));
        Assert.Empty(other.Items);

        var own = await service.ListAsync(asClientA, null, null, new ServiceTicketListFilter(ClientId: s.ClientA));
        Assert.Equal(3, own.Items.Count);
    }

    [Fact]
    public async Task ListAsync_EstadoInvalido_Es400_NoUn500()
    {
        var s = await SeedForListAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            BuildService(db).ListAsync(Staff(s.StaffUserId), null, null, new ServiceTicketListFilter(Status: "NoExiste")));
    }
}
