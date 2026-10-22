using Toner.Application.Assets;
using Toner.Application.Assets.Dtos;
using Toner.Application.Assets.Validators;
using Toner.Application.Common;
using Toner.Application.Maintenance;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Assets;

// Fin de la vida útil en alquiler: el modelo define a cuántas impresiones (contador total) el equipo sale de alquiler y se
// avisa "próxima" cuando faltan 50.000 (configurable por modelo). Es informativo: no bloquea nada.
public class RentalRetirementTests
{
    [Theory]
    [InlineData(500_000, 50_000, 100_000L, null, 400_000L)]      // lejos del límite: sin aviso
    [InlineData(500_000, 50_000, 450_000L, "Proxima", 50_000L)]  // faltan exactamente 50.000: ya avisa
    [InlineData(500_000, 50_000, 449_999L, null, 50_001L)]       // faltan 50.001: todavía no
    [InlineData(500_000, 50_000, 499_999L, "Proxima", 1L)]
    [InlineData(500_000, 50_000, 500_000L, "Superada", 0L)]      // llegó al límite
    [InlineData(500_000, 50_000, 530_000L, "Superada", -30_000L)] // se pasó 30.000
    [InlineData(500_000, 80_000, 430_000L, "Proxima", 70_000L)]   // ventana propia del modelo
    public void Evaluate_SegunLaDistanciaAlLimite(int threshold, int warning, long counter, string? status, long until)
    {
        var (s, u) = RentalRetirement.Evaluate(threshold, warning, counter);

        Assert.Equal(status, s);
        Assert.Equal(until, u);
    }

    [Fact]
    public void Evaluate_SinLimiteDelModelo_OSinLecturas_NoAvisaNiCalcula()
    {
        Assert.Equal((null, null), RentalRetirement.Evaluate(null, 50_000, 400_000));
        Assert.Equal((null, null), RentalRetirement.Evaluate(500_000, 50_000, null));
    }

    private sealed record World(string DbName, Asset Close, Asset Far, Asset NoLimit, Client Client, Guid ClientUserId);

    private static async Task<World> SeedAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var brand = TestEntities.AssetBrand();
        var limited = TestEntities.AssetModel(brand, "Con límite");
        limited.RentalRetirementPrintThreshold = 500_000;
        var unlimited = TestEntities.AssetModel(brand, "Sin límite");
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var close = TestEntities.Asset(limited, AssetLifecycleStatus.Instalado, location.Id);
        var far = TestEntities.Asset(limited, AssetLifecycleStatus.Instalado, location.Id);
        var noLimit = TestEntities.Asset(unlimited, AssetLifecycleStatus.Instalado, location.Id);
        var role = TestEntities.Role(RoleNames.Cliente);
        var clientUser = TestEntities.User(role, clientId: client.Id);
        db.AddRange(city, brand, limited, unlimited, client, location, close, far, noLimit, role, clientUser);
        db.AddRange(
            new MeterReading { AssetId = close.Id, CounterValue = 470_000, ReadingDate = DateTime.UtcNow.AddDays(-30) },
            new MeterReading { AssetId = close.Id, CounterValue = 480_000 }, // la última manda
            new MeterReading { AssetId = far.Id, CounterValue = 100_000 },
            new MeterReading { AssetId = noLimit.Id, CounterValue = 900_000 });
        await db.SaveChangesAsync();
        return new World(dbName, close, far, noLimit, client, clientUser.Id);
    }

    private static AssetService Service(Infrastructure.Persistence.TonerDbContext db) =>
        new(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));

    private static RequestingUser Staff() => new(Guid.NewGuid(), RoleNames.Coordinador, null, null);

    [Fact]
    public async Task Listado_MarcaComoProximaSoloLaQueEstaCercaDelLimite_UsandoLaUltimaLectura()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);

        var items = (await Service(db).ListAsync(Staff(), null, null)).Items.ToDictionary(a => a.Id);

        Assert.Equal("Proxima", items[w.Close.Id].RentalRetirementStatus);
        Assert.Equal(20_000, items[w.Close.Id].PrintsUntilRetirement);
        Assert.Null(items[w.Far.Id].RentalRetirementStatus);
        Assert.Equal(400_000, items[w.Far.Id].PrintsUntilRetirement);
        // Modelo sin límite: aunque tenga muchísimas impresiones, no hay nada que avisar.
        Assert.Null(items[w.NoLimit.Id].RentalRetirementStatus);
        Assert.Null(items[w.NoLimit.Id].PrintsUntilRetirement);
        Assert.Null(items[w.NoLimit.Id].RentalRetirementPrintThreshold);
    }

    [Fact]
    public async Task Detalle_TraeElMismoEstado()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);

        var detail = await Service(db).GetByIdAsync(Staff(), w.Close.Id);

        Assert.Equal("Proxima", detail.RentalRetirementStatus);
        Assert.Equal(500_000, detail.RentalRetirementPrintThreshold);
        Assert.Equal(50_000, detail.RentalRetirementWarningPrints);
    }

    [Fact]
    public async Task ElCliente_NuncaVeElRetiroDeAlquiler_NiEnListaNiEnDetalle()
    {
        var w = await SeedAsync();
        var client = new RequestingUser(w.ClientUserId, RoleNames.Cliente, w.Client.Id, null);
        using var db = TonerTestDb.CreateContext(w.DbName);
        var service = Service(db);

        foreach (var item in (await service.ListAsync(client, null, null)).Items)
        {
            Assert.Null(item.RentalRetirementStatus);
            Assert.Null(item.PrintsUntilRetirement);
            Assert.Null(item.RentalRetirementPrintThreshold);
        }

        var detail = await service.GetByIdAsync(client, w.Close.Id);
        Assert.Null(detail.RentalRetirementStatus);
        Assert.Null(detail.PrintsUntilRetirement);
        Assert.Null(detail.RentalRetirementPrintThreshold);
    }

    [Fact]
    public async Task LecturaDeContadores_TraeElEstadoParaElTecnico()
    {
        var w = await SeedAsync();
        using var db = TonerTestDb.CreateContext(w.DbName);

        var rows = (await Service(db).ListForMeterReadingAsync(Staff(), null, null)).Items.ToDictionary(r => r.AssetId);

        Assert.Equal("Proxima", rows[w.Close.Id].RentalRetirementStatus);
        Assert.Equal(20_000, rows[w.Close.Id].PrintsUntilRetirement);
        Assert.Null(rows[w.NoLimit.Id].RentalRetirementStatus);
    }

    [Fact]
    public void ValidadorDelModelo_ElLimiteEsOpcional_PeroSiSePoneDebeSerPositivo()
    {
        var validator = new CreateAssetModelRequestValidator();

        Assert.True(validator.Validate(new CreateAssetModelRequest { Name = "X", RentalRetirementPrintThreshold = null }).IsValid);
        Assert.True(validator.Validate(new CreateAssetModelRequest { Name = "X", RentalRetirementPrintThreshold = 500_000 }).IsValid);
        Assert.False(validator.Validate(new CreateAssetModelRequest { Name = "X", RentalRetirementPrintThreshold = 0 }).IsValid);
        Assert.False(validator.Validate(new CreateAssetModelRequest { Name = "X", RentalRetirementPrintThreshold = -5 }).IsValid);
        Assert.False(validator.Validate(new CreateAssetModelRequest { Name = "X", RentalRetirementWarningPrints = 0 }).IsValid);
    }

    [Fact]
    public async Task ElModelo_GuardaYDevuelveElLimite_AlCrearYAlEditar()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid brandId;
        using (var seed = TonerTestDb.CreateContext(dbName))
        {
            var brand = TestEntities.AssetBrand();
            seed.Add(brand);
            await seed.SaveChangesAsync();
            brandId = brand.Id;
        }

        using var db = TonerTestDb.CreateContext(dbName);
        var models = new AssetModelService(db, TestCache.New());
        var created = await models.CreateAsync(brandId, new CreateAssetModelRequest { Name = "M1", RentalRetirementPrintThreshold = 600_000, RentalRetirementWarningPrints = 40_000 });
        Assert.Equal(600_000, created.RentalRetirementPrintThreshold);
        Assert.Equal(40_000, created.RentalRetirementWarningPrints);

        var updated = await models.UpdateAsync(brandId, created.Id, new UpdateAssetModelRequest
        {
            Name = "M1", GeneralPrintThreshold = 1, GeneralMonthsInterval = 1, UnitsPrintThreshold = 1, UnitsMonthsInterval = 1,
            ConsumablesPrintThreshold = 1, RentalRetirementPrintThreshold = null
        });
        Assert.Null(updated.RentalRetirementPrintThreshold);
        Assert.Equal(50_000, updated.RentalRetirementWarningPrints);
    }
}
