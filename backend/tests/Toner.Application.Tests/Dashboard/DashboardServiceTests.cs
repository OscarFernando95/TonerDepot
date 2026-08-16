using Toner.Application.Dashboard;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Dashboard;

public class DashboardServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_NoData_ReturnsNullAveragesNotZeroOrException()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var service = new DashboardService(db);

        var result = await service.GetSummaryAsync(30);

        Assert.Null(result.Mttr.AverageResolutionHours);
        Assert.Equal(0, result.Mttr.ResolvedTicketCount);
        Assert.Null(result.SlaCompliance.OverallCompliancePercentage);
        Assert.Null(result.MaintenanceCompliance.OnTimePercentage);
        Assert.Empty(result.TicketsByCity);
    }

    [Fact]
    public async Task GetSummaryAsync_ComputesMttrAsAverageResolutionHours()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);

        var now = DateTime.UtcNow;
        var ticket1 = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Cerrado, ServiceTicketPriority.Media);
        ticket1.CreatedAt = now.AddHours(-10);
        ticket1.ResolvedAt = now.AddHours(-8); // 2 horas de resolución

        var ticket2 = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Cerrado, ServiceTicketPriority.Media);
        ticket2.CreatedAt = now.AddHours(-10);
        ticket2.ResolvedAt = now.AddHours(-4); // 6 horas de resolución

        arrangeDb.AddRange(role, user, city, client, location, ticket1, ticket2);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new DashboardService(actDb);

        var result = await service.GetSummaryAsync(30);

        Assert.Equal(2, result.Mttr.ResolvedTicketCount);
        Assert.Equal(4.0, result.Mttr.AverageResolutionHours); // promedio de 2h y 6h
    }

    [Fact]
    public async Task GetSummaryAsync_ComputesSlaComplianceForCriticalPriority()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);

        var now = DateTime.UtcNow;
        // Crítica: meta 4h. Uno dentro (3h), uno fuera (6h).
        var withinSla = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Cerrado, ServiceTicketPriority.Critica);
        withinSla.CreatedAt = now.AddHours(-10);
        withinSla.ResolvedAt = now.AddHours(-7);

        var outsideSla = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Cerrado, ServiceTicketPriority.Critica);
        outsideSla.CreatedAt = now.AddHours(-10);
        outsideSla.ResolvedAt = now.AddHours(-4);

        arrangeDb.AddRange(role, user, city, client, location, withinSla, outsideSla);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new DashboardService(actDb);

        var result = await service.GetSummaryAsync(30);

        var criticalRow = Assert.Single(result.SlaCompliance.ByPriority, p => p.Priority == nameof(ServiceTicketPriority.Critica));
        Assert.Equal(4, criticalRow.TargetHours);
        Assert.Equal(2, criticalRow.ResolvedCount);
        Assert.Equal(1, criticalRow.WithinSlaCount);
        Assert.Equal(50.0, criticalRow.CompliancePercentage);
    }

    [Fact]
    public async Task GetSummaryAsync_ComputesMaintenanceComplianceWithinWindow()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(asset, contract);

        var now = DateTime.UtcNow;
        var onTimeOrder = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Completada, scheduledDate: now.AddDays(-5));
        onTimeOrder.CompletedAt = now.AddDays(-3); // dentro de ScheduledDate + 3 días

        var lateOrder = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Completada, scheduledDate: now.AddDays(-10));
        lateOrder.CompletedAt = now.AddDays(-1); // 9 días tarde, fuera de la ventana de 3 días

        arrangeDb.AddRange(client, contract, brand, model, asset, schedule, onTimeOrder, lateOrder);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new DashboardService(actDb);

        var result = await service.GetSummaryAsync(30);

        Assert.Equal(2, result.MaintenanceCompliance.CompletedCount);
        Assert.Equal(1, result.MaintenanceCompliance.OnTimeCount);
        Assert.Equal(50.0, result.MaintenanceCompliance.OnTimePercentage);
    }

    [Fact]
    public async Task GetSummaryAsync_GroupsOpenAndUnassignedTicketsByCity()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City("Bogotá");
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);

        var openTicket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Abierto);
        var unassignedTicket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.SinAsignar);
        // Un ticket ya resuelto no debe contar en el backlog.
        var resolvedTicket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Resuelto);

        arrangeDb.AddRange(role, user, city, client, location, openTicket, unassignedTicket, resolvedTicket);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new DashboardService(actDb);

        var result = await service.GetSummaryAsync(30);

        var cityRow = Assert.Single(result.TicketsByCity);
        Assert.Equal("Bogotá", cityRow.CityName);
        Assert.Equal(1, cityRow.OpenCount);
        Assert.Equal(1, cityRow.UnassignedCount);
    }

    [Fact]
    public async Task GetSummaryAsync_ComputesTechnicianUtilizationAsPercentageOfAssumedCapacity()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser);

        var now = DateTime.UtcNow;
        var log = new TimeLog
        {
            TechnicianId = technician.Id,
            StartTime = now.AddDays(-1),
            EndTime = now.AddDays(-1).AddHours(4)
        };

        arrangeDb.AddRange(techRole, techUser, technician, log);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = new DashboardService(actDb);

        // Periodo de 10 días -> base asumida de 80h; 4h registradas -> 5% de utilización.
        var result = await service.GetSummaryAsync(10);

        var row = Assert.Single(result.TechnicianUtilization);
        Assert.Equal(4.0, row.HoursLogged);
        Assert.Equal(5.0, row.UtilizationPercentage);
    }
}
