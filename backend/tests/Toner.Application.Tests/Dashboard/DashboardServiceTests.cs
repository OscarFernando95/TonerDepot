using Toner.Application.Common.Interfaces;
using Toner.Application.Dashboard;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Dashboard;

public class DashboardServiceTests
{
    // Caché NUEVA por invocación: si los tests compartieran una, el resultado de uno se filtraría al
    // siguiente y estarían midiendo la caché en vez de la lógica del dashboard.
    private static DashboardService BuildService(Infrastructure.Persistence.TonerDbContext db) =>
        new(db, TestCache.New(), TestCache.StaffTenant(), TestCalendar.For(db));

    [Fact]
    public async Task GetSummaryAsync_NoData_ReturnsNullAveragesNotZeroOrException()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var service = BuildService(db);

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
        var service = BuildService(actDb);

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
        // 24/7: el SLA cuenta horas corridas, así el resultado no depende de la hora en que corra el test.
        client.SupportCoverage = SupportCoverage.Continuo24x7;
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
        var service = BuildService(actDb);

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
        var service = BuildService(actDb);

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
        var service = BuildService(actDb);

        var result = await service.GetSummaryAsync(30);

        var cityRow = Assert.Single(result.TicketsByCity);
        Assert.Equal("Bogotá", cityRow.CityName);
        Assert.Equal(1, cityRow.OpenCount);
        Assert.Equal(1, cityRow.UnassignedCount);
    }

    [Fact]
    public async Task GetSummaryAsync_ComputesTechnicianUtilizationAsPercentageOfScheduledHours()
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
        var service = BuildService(actDb);

        // La base ya no es un 8 h/día supuesto sino las horas laborales reales del técnico en el período
        // (horario por defecto lun-vie, sin festivos de Colombia). Se calcula con el mismo calendario para
        // que el test no dependa del día en que corra.
        var result = await service.GetSummaryAsync(10);

        var calendar = await TestCalendar.For(actDb).LoadAsync(new[] { technician.Id }, now.AddDays(-10), now.AddMinutes(1));
        var scheduledHours = calendar.BusinessHours(technician.Id, SupportCoverage.HorarioOficina, now.AddDays(-10), now);
        Assert.InRange(scheduledHours, 40, 80); // 10 días corridos = 6-8 días hábiles menos festivos posibles

        var row = Assert.Single(result.TechnicianUtilization);
        Assert.Equal(4.0, row.HoursLogged);
        Assert.Equal(Math.Round(4.0 / scheduledHours * 100, 1), row.UtilizationPercentage, 1);
    }

    [Fact]
    public async Task GetSummaryAsync_ClienteDeOficina_ElSlaNoCuentaLasHorasDeUnSabado()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var officeClient = TestEntities.Client("Oficina");
        var officeLocation = TestEntities.ClientLocation(officeClient, city);
        var alwaysClient = TestEntities.Client("24x7");
        alwaysClient.SupportCoverage = SupportCoverage.Continuo24x7;
        var alwaysLocation = TestEntities.ClientLocation(alwaysClient, city);

        // Último sábado 10:00 (hora de Bogotá): 6 h corridas contra una meta Crítica de 4 h, pero un
        // sábado no es horario laboral, así que para el cliente de oficina consumen 0 horas de SLA.
        var bogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, bogota);
        var daysBack = ((int)localNow.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7 + 7; // sábado de hace 7-13 días
        var saturday = TimeZoneInfo.ConvertTimeToUtc(localNow.Date.AddDays(-daysBack).AddHours(10), bogota);

        var office = TestEntities.ServiceTicket(officeLocation, user, ServiceTicketStatus.Cerrado, ServiceTicketPriority.Critica);
        office.CreatedAt = saturday;
        office.ResolvedAt = saturday.AddHours(6);
        var always = TestEntities.ServiceTicket(alwaysLocation, user, ServiceTicketStatus.Cerrado, ServiceTicketPriority.Critica);
        always.CreatedAt = saturday;
        always.ResolvedAt = saturday.AddHours(6);

        arrangeDb.AddRange(role, user, city, officeClient, officeLocation, alwaysClient, alwaysLocation, office, always);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var result = await BuildService(actDb).GetSummaryAsync(30);

        var critical = Assert.Single(result.SlaCompliance.ByPriority, p => p.Priority == nameof(ServiceTicketPriority.Critica));
        Assert.Equal(2, critical.ResolvedCount);
        Assert.Equal(1, critical.WithinSlaCount); // solo el de oficina: 0 h hábiles <= 4 h; el 24/7 usó 6 h corridas
    }
}
