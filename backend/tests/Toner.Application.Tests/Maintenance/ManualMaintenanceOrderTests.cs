using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Exceptions;
using Toner.Application.Maintenance;
using Toner.Application.Maintenance.Dtos;
using Toner.Application.Maintenance.Validators;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Maintenance;

// Mantenimiento a demanda: el staff pide una orden para un equipo instalado sin esperar a los umbrales del cronograma.
public class ManualMaintenanceOrderTests
{
    private sealed record Scenario(string DbName, Guid AssetId, Guid ScheduleId, Guid TechnicianId, Guid CityId);

    private static async Task<Scenario> SeedAsync(
        AssetLifecycleStatus status = AssetLifecycleStatus.Instalado, bool withSchedule = true, bool withCoverage = true)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var location = TestEntities.ClientLocation(client, city);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, status, location.Id);
        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);
        db.AddRange(city, client, contract, location, brand, model, asset, techRole, techUser, technician);

        var schedule = TestEntities.MaintenanceSchedule(asset, contract, nextConsumablesDueCounter: 500_000);
        schedule.ClientId = client.Id;
        if (withSchedule) db.Add(schedule);
        if (withCoverage) db.Add(new TechnicianCoverage { TechnicianId = technician.Id, CityId = city.Id });

        await db.SaveChangesAsync();
        return new Scenario(dbName, asset.Id, schedule.Id, technician.Id, city.Id);
    }

    private static MaintenanceOrderService Service(Infrastructure.Persistence.TonerDbContext db) =>
        new(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));

    private static CreateManualMaintenanceOrderRequest Request(Scenario s, bool general = true, bool units = false, bool consumables = false, string? reason = "Falla reportada") => new()
    {
        AssetId = s.AssetId, IncludesGeneral = general, IncludesUnits = units, IncludesConsumables = consumables, Reason = reason
    };

    [Fact]
    public async Task CreaLaOrdenManual_MarcadaYConSuMotivo_YLaAsignaAlTecnicoDisponible()
    {
        var s = await SeedAsync();
        var userId = Guid.NewGuid();
        using var db = TonerTestDb.CreateContext(s.DbName);

        var dto = await Service(db).CreateManualAsync(Request(s, general: true, units: true, reason: "  Falla de arrastre  "), userId);

        Assert.True(dto.IsManual);
        Assert.Equal("Falla de arrastre", dto.Reason);
        Assert.True(dto.IncludesGeneral && dto.IncludesUnits && !dto.IncludesConsumables);
        Assert.Equal(nameof(MaintenanceOrderStatus.Asignada), dto.Status);
        Assert.Equal(s.TechnicianId, dto.TechnicianId);

        using var check = TonerTestDb.CreateContext(s.DbName);
        var order = await check.MaintenanceOrders.SingleAsync();
        Assert.Equal(userId, order.RequestedByUserId);
        Assert.Equal(s.ScheduleId, order.MaintenanceScheduleId);
        Assert.Single(await check.AssignmentHistories.ToListAsync());
    }

    [Fact]
    public async Task SinTecnicoDisponible_QuedaPendiente_ParaQueElJobLaRecoja()
    {
        var s = await SeedAsync(withCoverage: false);
        using var db = TonerTestDb.CreateContext(s.DbName);

        var dto = await Service(db).CreateManualAsync(Request(s), Guid.NewGuid());

        Assert.Equal(nameof(MaintenanceOrderStatus.Pendiente), dto.Status);
        Assert.Null(dto.TechnicianId);
    }

    [Theory]
    [InlineData(AssetLifecycleStatus.EnBodega)]
    [InlineData(AssetLifecycleStatus.PendienteInstalacion)]
    [InlineData(AssetLifecycleStatus.DadoDeBaja)]
    public async Task ActivoNoInstalado_SeRechaza(AssetLifecycleStatus status)
    {
        var s = await SeedAsync(status);
        using var db = TonerTestDb.CreateContext(s.DbName);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Service(db).CreateManualAsync(Request(s), Guid.NewGuid()));
        Assert.Contains("instalado", ex.Message);
    }

    [Fact]
    public async Task ActivoSinCronogramaActivo_SeRechaza()
    {
        var s = await SeedAsync(withSchedule: false);
        using var db = TonerTestDb.CreateContext(s.DbName);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Service(db).CreateManualAsync(Request(s), Guid.NewGuid()));
        Assert.Contains("cronograma", ex.Message);
    }

    [Fact]
    public async Task ConUnaOrdenAbierta_SeRechaza_PeroTrasCancelarlaSePuedePedirOtra()
    {
        var s = await SeedAsync();
        Guid firstId;
        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            firstId = (await Service(db).CreateManualAsync(Request(s), Guid.NewGuid())).Id;
        }

        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            var ex = await Assert.ThrowsAsync<ConflictException>(() => Service(db).CreateManualAsync(Request(s), Guid.NewGuid()));
            Assert.Contains("abierta", ex.Message);
            await Service(db).CancelAsync(firstId);
        }

        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            await Service(db).CreateManualAsync(Request(s), Guid.NewGuid());
        }
    }

    [Fact]
    public async Task ActivoInexistente_DaNotFound()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service(db).CreateManualAsync(new CreateManualMaintenanceOrderRequest { AssetId = Guid.NewGuid(), IncludesGeneral = true }, Guid.NewGuid()));
    }

    [Fact]
    public async Task CompletarUnaOrdenManualSoloDeInsumos_NoTocaElMantenimientoGeneral()
    {
        var s = await SeedAsync();
        Guid orderId;
        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            orderId = (await Service(db).CreateManualAsync(Request(s, general: false, consumables: true), Guid.NewGuid())).Id;
        }

        DateTime generalDueBefore;
        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            generalDueBefore = (await db.MaintenanceSchedules.SingleAsync()).NextGeneralDueAt;
            await Service(db).CompleteAsync(orderId, new CompleteMaintenanceOrderRequest { CounterValue = 12_000 }, Guid.NewGuid());
        }

        using var check = TonerTestDb.CreateContext(s.DbName);
        var schedule = await check.MaintenanceSchedules.SingleAsync();
        Assert.Equal(generalDueBefore, schedule.NextGeneralDueAt);
        Assert.Equal(12_000, schedule.LastConsumablesChangeCounter);
        Assert.Null(schedule.LastGeneralMaintenanceCounter);
    }

    [Fact]
    public void Validador_ExigeAlMenosUnTipo_YAcotaElMotivo()
    {
        var validator = new CreateManualMaintenanceOrderRequestValidator();

        Assert.False(validator.Validate(new CreateManualMaintenanceOrderRequest { AssetId = Guid.NewGuid() }).IsValid);
        Assert.False(validator.Validate(new CreateManualMaintenanceOrderRequest { AssetId = Guid.Empty, IncludesUnits = true }).IsValid);
        Assert.False(validator.Validate(new CreateManualMaintenanceOrderRequest { AssetId = Guid.NewGuid(), IncludesUnits = true, Reason = new string('x', 501) }).IsValid);
        Assert.True(validator.Validate(new CreateManualMaintenanceOrderRequest { AssetId = Guid.NewGuid(), IncludesConsumables = true }).IsValid);
    }
}
