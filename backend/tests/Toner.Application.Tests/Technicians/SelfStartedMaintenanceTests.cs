using Microsoft.EntityFrameworkCore;
using Toner.Application.Assignment;
using Toner.Application.Common.Exceptions;
using Toner.Application.Maintenance;
using Toner.Application.Maintenance.Dtos;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Technicians;

// Mantenimiento a demanda iniciado por el propio técnico: la orden nace asignada a él, solo sobre equipos de su alcance
// y dentro de su horario, y al cerrar la visita el cronograma reinicia únicamente lo que eligió.
public class SelfStartedMaintenanceTests
{
    private sealed record Scenario(string DbName, Guid AssetId, Guid ScheduleId, Guid TechnicianId, Guid UserId, Guid ClientId);

    private static readonly DateTime LastDone = new(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

    private static async Task<Scenario> SeedAsync(
        bool withCoverage = true, bool linked = false, TechnicianStatus status = TechnicianStatus.Disponible,
        AssetLifecycleStatus assetStatus = AssetLifecycleStatus.Instalado, SupportCoverage coverage = SupportCoverage.HorarioOficina)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        client.SupportCoverage = coverage;
        var contract = TestEntities.Contract(client);
        var location = TestEntities.ClientLocation(client, city);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, assetStatus, location.Id);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: status);
        db.AddRange(city, client, contract, location, brand, model, asset, techRole, techUser, technician);

        // Cronograma con historial real: lo que se reinicie tiene que verse distinto a lo que ya traía.
        var schedule = TestEntities.MaintenanceSchedule(
            asset, contract,
            nextGeneralDueAt: new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc), nextGeneralDueCounter: 30_000,
            nextUnitsDueAt: new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc), nextUnitsDueCounter: 30_000,
            nextConsumablesDueCounter: 60_000,
            lastGeneralMaintenanceAt: LastDone, lastGeneralMaintenanceCounter: 0,
            lastUnitsMaintenanceAt: LastDone, lastUnitsMaintenanceCounter: 0,
            lastConsumablesChangeAt: LastDone, lastConsumablesChangeCounter: 0);
        schedule.ClientId = client.Id;
        db.Add(schedule);

        if (withCoverage) db.Add(TestEntities.Coverage(technician, city));
        if (linked) db.Add(new TechnicianAsset { TechnicianId = technician.Id, AssetId = asset.Id });

        await db.SaveChangesAsync();
        return new Scenario(dbName, asset.Id, schedule.Id, technician.Id, techUser.Id, client.Id);
    }

    private static CreateManualMaintenanceOrderRequest Request(
        Scenario s, bool general = false, bool units = true, bool consumables = false, string? reason = "Preventivo en sitio") => new()
    {
        AssetId = s.AssetId, IncludesGeneral = general, IncludesUnits = units, IncludesConsumables = consumables, Reason = reason
    };

    private static Task<MaintenanceOrderDto> StartAsync(Scenario s, CreateManualMaintenanceOrderRequest request, TimeProvider? time = null)
    {
        var db = TonerTestDb.CreateContext(s.DbName);
        return TestCheckIn.Create(db, time: time).StartMaintenanceAsync(s.TechnicianId, request);
    }

    [Fact]
    public async Task Inicia_LaOrdenNaceAsignadaAlTecnico_ConLoQueElEligio_YSuHistorial()
    {
        var s = await SeedAsync();

        var dto = await StartAsync(s, Request(s, general: true, units: true, reason: "  Cambio de unidades  "));

        Assert.Equal(nameof(MaintenanceOrderStatus.Asignada), dto.Status);
        Assert.Equal(s.TechnicianId, dto.TechnicianId);
        Assert.True(dto.IsManual);
        Assert.True(dto.IncludesGeneral && dto.IncludesUnits && !dto.IncludesConsumables);
        Assert.Equal("Cambio de unidades", dto.Reason);

        using var check = TonerTestDb.CreateContext(s.DbName);
        var order = await check.MaintenanceOrders.SingleAsync();
        Assert.Equal(s.UserId, order.RequestedByUserId);
        Assert.Equal(s.ClientId, order.ClientId);
        Assert.Equal(s.ScheduleId, order.MaintenanceScheduleId);
        var history = await check.AssignmentHistories.SingleAsync();
        Assert.Equal(AssignmentType.IniciadaPorTecnico, history.AssignmentType);
        Assert.Equal(s.TechnicianId, history.TechnicianId);
        // Iniciar no lo pone Ocupado: eso lo hace el check-in. Hasta entonces sigue disponible para otro trabajo.
        Assert.Equal(TechnicianStatus.Disponible, (await check.Technicians.SingleAsync()).Status);
    }

    [Fact]
    public async Task ElFlujoCompleto_IniciarCheckInYCheckOut_ReiniciaSoloLoQueEligio()
    {
        var s = await SeedAsync();
        var dto = await StartAsync(s, Request(s, general: false, units: true, consumables: false));

        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            await TestCheckIn.Create(db).CheckInAsync(s.TechnicianId, new CheckInRequest { MaintenanceOrderId = dto.Id });
        }

        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            await TestCheckIn.Create(db).CheckOutAsync(s.TechnicianId, new CheckOutRequest
            {
                Resolved = true, InitialCounterValue = 12_000, InitialCounterDate = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc)
            });
        }

        using var check = TonerTestDb.CreateContext(s.DbName);
        var order = await check.MaintenanceOrders.SingleAsync();
        Assert.Equal(MaintenanceOrderStatus.Completada, order.Status);
        Assert.NotNull(order.CompletedAt);

        var schedule = await check.MaintenanceSchedules.Include(x => x.Asset).ThenInclude(a => a.AssetModel).SingleAsync();
        var model = schedule.Asset.AssetModel;

        // Unidades: se reinició desde el contador y la fecha de esta visita.
        Assert.Equal(12_000, schedule.LastUnitsMaintenanceCounter);
        Assert.Equal(12_000 + model.UnitsPrintThreshold, schedule.NextUnitsDueCounter);
        Assert.Equal(new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc).AddMonths(model.UnitsMonthsInterval), schedule.NextUnitsDueAt);

        // General e insumos no los eligió: quedan EXACTAMENTE como estaban.
        Assert.Equal(0, schedule.LastGeneralMaintenanceCounter);
        Assert.Equal(30_000, schedule.NextGeneralDueCounter);
        Assert.Equal(new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc), schedule.NextGeneralDueAt);
        Assert.Equal(0, schedule.LastConsumablesChangeCounter);
        Assert.Equal(60_000, schedule.NextConsumablesDueCounter);

        Assert.Equal(TechnicianStatus.Disponible, (await check.Technicians.SingleAsync()).Status);
        Assert.Equal(12_000, (await check.MeterReadings.SingleAsync()).CounterValue);
    }

    [Fact]
    public async Task CambiarInsumos_TambienReiniciaUnidades_ComoEnCualquierOtraOrden()
    {
        var s = await SeedAsync();
        var dto = await StartAsync(s, Request(s, general: false, units: false, consumables: true));

        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            await TestCheckIn.Create(db).CheckInAsync(s.TechnicianId, new CheckInRequest { MaintenanceOrderId = dto.Id });
        }

        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            await TestCheckIn.Create(db).CheckOutAsync(s.TechnicianId, new CheckOutRequest { Resolved = true, InitialCounterValue = 9_000 });
        }

        using var check = TonerTestDb.CreateContext(s.DbName);
        var schedule = await check.MaintenanceSchedules.Include(x => x.Asset).ThenInclude(a => a.AssetModel).SingleAsync();
        Assert.Equal(9_000, schedule.LastConsumablesChangeCounter);
        Assert.Equal(9_000 + schedule.Asset.AssetModel.ConsumablesPrintThreshold, schedule.NextConsumablesDueCounter);
        Assert.Equal(9_000, schedule.LastUnitsMaintenanceCounter);
        Assert.Equal(0, schedule.LastGeneralMaintenanceCounter);
    }

    [Fact]
    public async Task EquipoFueraDeSuZonaYSinVinculo_SeRechaza_YNoSeCreaNada()
    {
        var s = await SeedAsync(withCoverage: false);

        await Assert.ThrowsAsync<ForbiddenException>(() => StartAsync(s, Request(s)));

        using var check = TonerTestDb.CreateContext(s.DbName);
        Assert.Empty(await check.MaintenanceOrders.ToListAsync());
        Assert.Empty(await check.AssignmentHistories.ToListAsync());
    }

    [Fact]
    public async Task EquipoVinculadoAMano_AunqueNoEsteEnSuZona_SePuede()
    {
        var s = await SeedAsync(withCoverage: false, linked: true);

        var dto = await StartAsync(s, Request(s));

        Assert.Equal(s.TechnicianId, dto.TechnicianId);
    }

    [Fact]
    public async Task ConUnaVisitaEnCurso_SeRechaza()
    {
        var s = await SeedAsync(status: TechnicianStatus.Ocupado);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => StartAsync(s, Request(s)));
        Assert.Contains("visita en curso", ex.Message);
    }

    [Fact]
    public async Task FueraDeHorario_ClienteDeHorarioOficina_SeRechaza_PeroUnClienteContinuoNo()
    {
        var office = await SeedAsync(coverage: SupportCoverage.HorarioOficina);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => StartAsync(office, Request(office), FixedTimeProvider.Weekend));
        Assert.Contains("horario laboral", ex.Message);

        var always = await SeedAsync(coverage: SupportCoverage.Continuo24x7);
        var dto = await StartAsync(always, Request(always), FixedTimeProvider.Weekend);
        Assert.Equal(always.TechnicianId, dto.TechnicianId);
    }

    [Theory]
    [InlineData(AssetLifecycleStatus.EnBodega)]
    [InlineData(AssetLifecycleStatus.PendienteInstalacion)]
    [InlineData(AssetLifecycleStatus.DadoDeBaja)]
    public async Task EquipoNoInstalado_SeRechaza(AssetLifecycleStatus assetStatus)
    {
        var s = await SeedAsync(linked: true, assetStatus: assetStatus);

        await Assert.ThrowsAsync<ConflictException>(() => StartAsync(s, Request(s)));
    }

    [Fact]
    public async Task ConUnaOrdenYaAbierta_SeRechaza_ParaNoDuplicarLaDelCronograma()
    {
        var s = await SeedAsync();
        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            var schedule = await db.MaintenanceSchedules.SingleAsync();
            var asset = await db.Assets.SingleAsync();
            var open = TestEntities.MaintenanceOrder(schedule, asset);
            open.ClientId = s.ClientId;
            db.Add(open);
            await db.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<ConflictException>(() => StartAsync(s, Request(s)));
        Assert.Contains("orden de mantenimiento abierta", ex.Message);

        using var check = TonerTestDb.CreateContext(s.DbName);
        Assert.Single(await check.MaintenanceOrders.ToListAsync());
    }

    [Fact]
    public async Task MientrasLaOrdenEstaAbierta_ElMotorDelCronogramaNoGeneraOtraEncima_NiLaReasignacionPendienteLaToca()
    {
        var s = await SeedAsync();
        var dto = await StartAsync(s, Request(s));

        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            // Aunque el contador ya pase todos los umbrales, el motor respeta la orden abierta.
            var evaluated = await new MaintenanceScheduleEngine(db).EvaluateAsync(s.AssetId, 999_999, DateTime.UtcNow);
            Assert.Null(evaluated);

            // El job de reasignación solo recoge Pendiente sin técnico: la orden propia nace Asignada.
            var (_, orders) = await new PendingAssignmentService(db, TestAssignment.Create(db)).RunAsync();
            Assert.Equal(0, orders);
        }

        using var check = TonerTestDb.CreateContext(s.DbName);
        var order = await check.MaintenanceOrders.SingleAsync();
        Assert.Equal(dto.Id, order.Id);
        Assert.Equal(s.TechnicianId, order.TechnicianId);
        Assert.Single(await check.AssignmentHistories.ToListAsync());
    }

    [Fact]
    public async Task Cancelar_UnaOrdenPropiaAunSinCheckIn_LiberaElEquipo()
    {
        var s = await SeedAsync();
        var dto = await StartAsync(s, Request(s));

        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            var cancelled = await new MaintenanceOrderService(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db))
                .CancelSelfStartedAsync(dto.Id, s.TechnicianId);
            Assert.Equal(nameof(MaintenanceOrderStatus.Cancelada), cancelled.Status);
        }

        // Equipo libre otra vez: puede iniciar de nuevo.
        var again = await StartAsync(s, Request(s));
        Assert.Equal(nameof(MaintenanceOrderStatus.Asignada), again.Status);
    }

    [Fact]
    public async Task Cancelar_UnaOrdenYaEnProceso_SeRechaza()
    {
        var s = await SeedAsync();
        var dto = await StartAsync(s, Request(s));
        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            await TestCheckIn.Create(db).CheckInAsync(s.TechnicianId, new CheckInRequest { MaintenanceOrderId = dto.Id });
        }

        using var cancelDb = TonerTestDb.CreateContext(s.DbName);
        var service = new MaintenanceOrderService(cancelDb, new MaintenanceScheduleEngine(cancelDb), TestAssignment.Create(cancelDb));
        await Assert.ThrowsAsync<ConflictException>(() => service.CancelSelfStartedAsync(dto.Id, s.TechnicianId));
    }

    [Fact]
    public async Task Cancelar_UnaOrdenDelCronogramaAsignadaAlTecnico_SeRechaza_CancelarlaEsDelStaff()
    {
        var s = await SeedAsync();
        Guid orderId;
        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            var schedule = await db.MaintenanceSchedules.SingleAsync();
            var asset = await db.Assets.SingleAsync();
            var scheduled = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Asignada, s.TechnicianId);
            scheduled.ClientId = s.ClientId;
            db.Add(scheduled);
            await db.SaveChangesAsync();
            orderId = scheduled.Id;
        }

        using var cancelDb = TonerTestDb.CreateContext(s.DbName);
        var service = new MaintenanceOrderService(cancelDb, new MaintenanceScheduleEngine(cancelDb), TestAssignment.Create(cancelDb));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CancelSelfStartedAsync(orderId, s.TechnicianId));
    }

    [Fact]
    public async Task Cancelar_LaOrdenPropiaDeOtroTecnico_SeRechaza()
    {
        var s = await SeedAsync();
        var dto = await StartAsync(s, Request(s));
        Guid otherTechnicianId;
        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            var role = await db.Roles.SingleAsync(r => r.Name == RoleNames.Tecnico);
            var otherUser = TestEntities.User(role);
            var other = TestEntities.Technician(otherUser);
            db.AddRange(otherUser, other);
            await db.SaveChangesAsync();
            otherTechnicianId = other.Id;
        }

        using var cancelDb = TonerTestDb.CreateContext(s.DbName);
        var service = new MaintenanceOrderService(cancelDb, new MaintenanceScheduleEngine(cancelDb), TestAssignment.Create(cancelDb));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CancelSelfStartedAsync(dto.Id, otherTechnicianId));
    }
}
