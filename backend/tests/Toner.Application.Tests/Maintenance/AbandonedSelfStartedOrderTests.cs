using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Toner.Application.Maintenance;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Maintenance;

// MANT-2: una orden que el técnico inició y nunca empezó bloquea al motor del cronograma para ese equipo; pasado el plazo
// caduca sola. Solo caduca la que sigue siendo "suya": si otro técnico la tomó o el staff la reasignó, ya no.
public class AbandonedSelfStartedOrderTests
{
    // FixedTimeProvider.WorkingHours = 2026-09-30 15:00 UTC; el plazo por defecto es de 4 horas.
    private static readonly DateTime Now = FixedTimeProvider.WorkingHours.GetUtcNow().UtcDateTime;

    private sealed record Scenario(string DbName, Guid ScheduleId, Guid AssetId, Guid TechnicianId, Guid ClientId);

    private static async Task<Scenario> SeedAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var location = TestEntities.ClientLocation(client, city);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, location.Id);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser);
        var schedule = TestEntities.MaintenanceSchedule(asset, contract, nextUnitsDueAt: Now.AddDays(-1));
        schedule.ClientId = client.Id;
        db.AddRange(city, client, contract, location, brand, model, asset, techRole, techUser, technician, schedule);
        await db.SaveChangesAsync();
        return new Scenario(dbName, schedule.Id, asset.Id, technician.Id, client.Id);
    }

    private static async Task<Guid> AddOrderAsync(
        Scenario s, MaintenanceOrderStatus status, DateTime assignedAt, params AssignmentType[] history)
    {
        using var db = TonerTestDb.CreateContext(s.DbName);
        var order = new MaintenanceOrder
        {
            MaintenanceScheduleId = s.ScheduleId, AssetId = s.AssetId, ClientId = s.ClientId, Status = status,
            ScheduledDate = assignedAt, IncludesUnits = true, IsManual = true, TechnicianId = s.TechnicianId
        };
        db.Add(order);
        foreach (var type in history)
        {
            db.Add(new AssignmentHistory
            {
                MaintenanceOrderId = order.Id, ClientId = s.ClientId, TechnicianId = s.TechnicianId, AssignmentType = type, AssignedAt = assignedAt
            });
        }

        await db.SaveChangesAsync();
        return order.Id;
    }

    private static async Task<int> RunAsync(Scenario s, int hours = 4)
    {
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = new AbandonedSelfStartedOrderService(
            db, Options.Create(new AbandonedSelfStartedOrderOptions { ExpireAfterHours = hours }),
            FixedTimeProvider.WorkingHours, NullLogger<AbandonedSelfStartedOrderService>.Instance);
        return await service.RunAsync();
    }

    private static async Task<MaintenanceOrderStatus> StatusOfAsync(Scenario s, Guid orderId)
    {
        using var db = TonerTestDb.CreateContext(s.DbName);
        return (await db.MaintenanceOrders.SingleAsync(o => o.Id == orderId)).Status;
    }

    [Fact]
    public async Task OrdenAutoiniciadaSinCheckInPasadoElPlazo_Caduca()
    {
        var s = await SeedAsync();
        var id = await AddOrderAsync(s, MaintenanceOrderStatus.Asignada, Now.AddHours(-5), AssignmentType.IniciadaPorTecnico);

        Assert.Equal(1, await RunAsync(s));
        Assert.Equal(MaintenanceOrderStatus.Cancelada, await StatusOfAsync(s, id));
    }

    [Fact]
    public async Task DentroDelPlazo_NoSeToca()
    {
        var s = await SeedAsync();
        var id = await AddOrderAsync(s, MaintenanceOrderStatus.Asignada, Now.AddHours(-3), AssignmentType.IniciadaPorTecnico);

        Assert.Equal(0, await RunAsync(s));
        Assert.Equal(MaintenanceOrderStatus.Asignada, await StatusOfAsync(s, id));
    }

    [Fact]
    public async Task ElPlazoSeConfigura()
    {
        var s = await SeedAsync();
        var id = await AddOrderAsync(s, MaintenanceOrderStatus.Asignada, Now.AddHours(-3), AssignmentType.IniciadaPorTecnico);

        Assert.Equal(1, await RunAsync(s, hours: 2));
        Assert.Equal(MaintenanceOrderStatus.Cancelada, await StatusOfAsync(s, id));
    }

    [Fact]
    public async Task UnaOrdenYaEnProceso_NuncaCaduca_AunqueSeaVieja()
    {
        var s = await SeedAsync();
        var id = await AddOrderAsync(s, MaintenanceOrderStatus.EnProceso, Now.AddDays(-2), AssignmentType.IniciadaPorTecnico);

        Assert.Equal(0, await RunAsync(s));
        Assert.Equal(MaintenanceOrderStatus.EnProceso, await StatusOfAsync(s, id));
    }

    [Theory]
    [InlineData(AssignmentType.Reclamada)]
    [InlineData(AssignmentType.Manual)]
    public async Task SiOtroLaTomoOElStaffLaReasigno_YaNoEsDeQuienLaInicio_NoCaduca(AssignmentType later)
    {
        var s = await SeedAsync();
        var id = await AddOrderAsync(s, MaintenanceOrderStatus.Asignada, Now.AddHours(-9), AssignmentType.IniciadaPorTecnico, later);

        Assert.Equal(0, await RunAsync(s));
        Assert.Equal(MaintenanceOrderStatus.Asignada, await StatusOfAsync(s, id));
    }

    [Theory]
    [InlineData(AssignmentType.Automatica)]
    [InlineData(AssignmentType.Manual)]
    public async Task LasOrdenesDelMotorOElStaff_NoCaducan(AssignmentType type)
    {
        var s = await SeedAsync();
        var id = await AddOrderAsync(s, MaintenanceOrderStatus.Asignada, Now.AddDays(-3), type);

        Assert.Equal(0, await RunAsync(s));
        Assert.Equal(MaintenanceOrderStatus.Asignada, await StatusOfAsync(s, id));
    }

    [Fact]
    public async Task AlCaducar_ElEquipoQuedaLibre_YElMotorDelCronogramaVuelveAGenerarSuOrden()
    {
        var s = await SeedAsync();
        await AddOrderAsync(s, MaintenanceOrderStatus.Asignada, Now.AddHours(-5), AssignmentType.IniciadaPorTecnico);

        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            Assert.Null(await new MaintenanceScheduleEngine(db).EvaluateAsync(s.AssetId, 0, Now));
        }

        await RunAsync(s);

        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            Assert.NotNull(await new MaintenanceScheduleEngine(db).EvaluateAsync(s.AssetId, 0, Now));
        }
    }

    private static async Task PlanAsync(Scenario s, Guid orderId, DateTime plannedAt)
    {
        using var db = TonerTestDb.CreateContext(s.DbName);
        (await db.MaintenanceOrders.SingleAsync(o => o.Id == orderId)).PlannedAt = plannedAt;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task UnaOrdenProgramadaParaMasAdelante_NoCaducaAunqueSeVieja()
    {
        // Creada hace 10 horas (pasado el plazo de 4), pero el técnico la programó para dentro de 5 días: no está abandonada.
        var s = await SeedAsync();
        var id = await AddOrderAsync(s, MaintenanceOrderStatus.Asignada, Now.AddHours(-10), AssignmentType.IniciadaPorTecnico);
        await PlanAsync(s, id, Now.AddDays(5));

        Assert.Equal(0, await RunAsync(s));
        Assert.Equal(MaintenanceOrderStatus.Asignada, await StatusOfAsync(s, id));
    }

    [Fact]
    public async Task UnaOrdenProgramada_CaducaSoloCuandoPasoElPlazoDesdeSuFecha()
    {
        var s = await SeedAsync();
        var recent = await AddOrderAsync(s, MaintenanceOrderStatus.Asignada, Now.AddDays(-3), AssignmentType.IniciadaPorTecnico);
        await PlanAsync(s, recent, Now.AddHours(-1)); // la hora pasó hace 1 h: todavía dentro del plazo de 4

        Assert.Equal(0, await RunAsync(s));
        Assert.Equal(MaintenanceOrderStatus.Asignada, await StatusOfAsync(s, recent));

        await PlanAsync(s, recent, Now.AddHours(-5)); // pasó hace 5 h sin check-in: abandonada
        Assert.Equal(1, await RunAsync(s));
        Assert.Equal(MaintenanceOrderStatus.Cancelada, await StatusOfAsync(s, recent));
    }
}
