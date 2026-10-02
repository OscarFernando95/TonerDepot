using Toner.Application.Inventory.Analytics;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Inventory;

// BI de tóner: la duración de un tóner es lo que recorre el contador de la máquina entre una entrega y la siguiente del
// mismo tóner (dividido entre las unidades de la entrega anterior).
public class TonerAnalyticsTests
{
    private static readonly Guid ModelM = Guid.NewGuid();
    private static readonly Guid Black = Guid.NewGuid();
    private static readonly Guid Cyan = Guid.NewGuid();
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();

    private static DateTime D(int month, int day) => new(2026, month, day, 12, 0, 0, DateTimeKind.Utc);

    private static Dictionary<Guid, TonerAssetInfo> Assets() => new()
    {
        [A] = new(A, ModelM, "Ricoh", "MP2553", "SER-A", "Cliente 1", "Sede 1", "Sur"),
        [B] = new(B, ModelM, "Ricoh", "MP2553", "SER-B", "Cliente 2", "Sede 2", "Norte")
    };

    private static TonerEvent E(Guid asset, Guid item, DateTime at, int units, long? counter, bool? delivered = false) =>
        new(asset, item, at, units, counter, delivered);

    [Fact]
    public void LaDuracion_EsElRecorridoDelContadorEntreEntregas_DivididoEntreLasUnidadesDeLaAnterior()
    {
        var events = new[]
        {
            E(A, Black, D(1, 1), 2, 1000),
            E(A, Black, D(3, 1), 1, 7000),   // cierra: 6000 páginas con 2 tóner = 3000 c/u
            E(A, Black, D(6, 1), 1, 10000)   // cierra: 3000 páginas con 1 tóner = 3000
        };

        var (machines, _) = TonerUsageCalculator.Compute(events, Assets(), D(2, 1), D(7, 1));

        var row = Assert.Single(machines);
        Assert.Equal(2, row.TotalUnits);          // entregas dentro del rango (marzo y junio)
        Assert.Equal(3, row.MeasuredUnits);       // los 2 + 1 tóner cuya duración quedó medida
        Assert.Equal(3000, row.AvgPagesPerUnit);
        Assert.Equal(9000, row.PagesInRange);     // del contador anterior al rango (1000) al último (10000)
        Assert.Equal(10000, row.LastCounter);
    }

    [Fact]
    public void ElRango_SoloContaLasEntregasDentro_PeroUsaLasAnterioresParaCerrarIntervalos()
    {
        var events = new[] { E(A, Black, D(1, 1), 2, 1000), E(A, Black, D(3, 1), 1, 7000), E(A, Black, D(6, 1), 1, 10000) };

        var (machines, _) = TonerUsageCalculator.Compute(events, Assets(), D(1, 1), D(3, 15));

        var row = Assert.Single(machines);
        Assert.Equal(3, row.TotalUnits);   // enero (2) y marzo (1)
        Assert.Equal(2, row.MeasuredUnits);
        Assert.Equal(3000, row.AvgPagesPerUnit);
    }

    [Fact]
    public void ElRendimiento_SeCompara_ConElPromedioDeOtrasMaquinasDelMismoModeloYTonerSeparandoColores()
    {
        var events = new[]
        {
            E(A, Black, D(1, 1), 2, 1000), E(A, Black, D(3, 1), 1, 7000), E(A, Black, D(6, 1), 1, 10000),
            E(B, Black, D(1, 15), 1, 0), E(B, Black, D(4, 1), 1, 2000)
        };

        var (machines, _) = TonerUsageCalculator.Compute(events, Assets(), D(2, 1), D(7, 1));

        // Promedio del modelo con este tóner: (6000 + 3000 + 2000) / (2 + 1 + 1) = 2750 páginas.
        var a = machines.Single(m => m.SerialNumber == "SER-A");
        var b = machines.Single(m => m.SerialNumber == "SER-B");
        Assert.Equal(109.1, a.VsModelPercent);   // 9000 reales / 8250 esperadas
        Assert.Equal(72.7, b.VsModelPercent);    // 2000 reales / 2750 esperadas
    }

    [Fact]
    public void UnaMaquinaSolaEnSuModelo_NoTieneComparacion()
    {
        var events = new[] { E(A, Black, D(1, 1), 1, 0), E(A, Black, D(3, 1), 1, 3000) };

        var (machines, _) = TonerUsageCalculator.Compute(events, Assets(), D(1, 1), D(7, 1));

        Assert.Null(Assert.Single(machines).VsModelPercent);
        Assert.Equal(3000, machines[0].AvgPagesPerUnit);
    }

    [Fact]
    public void LosColores_SeMidenPorSeparado_YNoMezclanLasPaginas()
    {
        var events = new[]
        {
            E(A, Black, D(1, 1), 1, 0), E(A, Black, D(3, 1), 1, 6000),     // negro: 6000 páginas
            E(A, Cyan, D(1, 1), 1, 0), E(A, Cyan, D(4, 1), 1, 8000)        // cian: 8000 páginas
        };

        var (machines, _) = TonerUsageCalculator.Compute(events, Assets(), D(1, 1), D(7, 1));

        var row = Assert.Single(machines);
        Assert.Equal(2, row.MeasuredUnits);
        Assert.Equal(7000, row.AvgPagesPerUnit);      // (6000 + 8000) / 2
        Assert.Equal(8000, row.PagesInRange);          // el contador es de la máquina: no se suma por color
    }

    [Fact]
    public void UnRegistroSinContador_CuentaUnidades_PeroNoMideDuracion()
    {
        var events = new[] { E(A, Black, D(1, 1), 1, 1000), E(A, Black, D(2, 1), 1, null), E(A, Black, D(3, 1), 1, 4000) };

        var (machines, _) = TonerUsageCalculator.Compute(events, Assets(), D(1, 1), D(7, 1));

        var row = Assert.Single(machines);
        Assert.Equal(3, row.TotalUnits);
        Assert.Equal(1, row.MeasuredUnits);        // solo el intervalo ene→mar (entre registros con contador)
        Assert.Equal(3000, row.AvgPagesPerUnit);
    }

    [Fact]
    public void UnContadorQueRetrocede_SeIgnora_SinRomperLoDemas()
    {
        var events = new[] { E(A, Black, D(1, 1), 1, 5000), E(A, Black, D(2, 1), 1, 4000), E(A, Black, D(3, 1), 1, 7000) };

        var (machines, _) = TonerUsageCalculator.Compute(events, Assets(), D(1, 1), D(7, 1));

        var row = Assert.Single(machines);
        Assert.Equal(1, row.MeasuredUnits);        // solo feb→mar (4000→7000); ene→feb retrocede y se descarta
        Assert.Equal(3000, row.AvgPagesPerUnit);
    }

    [Fact]
    public void SeparaLoCambiadoPorElTecnico_DeLoEntregadoAlUsuario_YAgrupaPorMes()
    {
        var events = new[]
        {
            E(A, Black, D(2, 3), 1, 100, delivered: false),
            E(A, Black, D(2, 20), 2, 200, delivered: true),
            E(A, Black, D(4, 5), 1, 300, delivered: true)
        };

        var (machines, monthly) = TonerUsageCalculator.Compute(events, Assets(), D(1, 1), D(7, 1));

        var row = Assert.Single(machines);
        Assert.Equal(1, row.ChangedByTechnicianUnits);
        Assert.Equal(3, row.DeliveredToUserUnits);
        Assert.Equal(new[] { ("2026-02", 3), ("2026-04", 1) }, monthly.Select(m => (m.Month, m.Units)));
    }

    [Fact]
    public void LaAgrupacion_PromediaPonderadoPorLosTonerMedidos()
    {
        var events = new[]
        {
            E(A, Black, D(1, 1), 1, 0), E(A, Black, D(2, 1), 1, 4000),     // A: 4000 con 1 tóner
            E(B, Black, D(1, 1), 3, 0), E(B, Black, D(2, 1), 1, 6000)      // B: 6000 con 3 tóner = 2000 c/u
        };

        var (machines, _) = TonerUsageCalculator.Compute(events, Assets(), D(1, 1), D(7, 1));
        var group = Assert.Single(TonerUsageCalculator.GroupBy(machines, _ => "Ricoh MP2553"));

        Assert.Equal(2, group.Machines);
        Assert.Equal(2500, group.AvgPagesPerUnit);   // (4000 + 6000) / (1 + 3)
    }
}

public class TonerAnalyticsServiceTests
{
    private sealed record Setup(string DbName, Guid ClientA, Guid ClientB);

    private static async Task<Setup> SeedAsync(string clientAName = "Cliente A")
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var zone = new Zone { Name = "Sur" };
        var city = TestEntities.City();
        city.ZoneId = zone.Id;
        var clientA = TestEntities.Client(clientAName);
        var clientB = TestEntities.Client("Cliente B");
        var locA = TestEntities.ClientLocation(clientA, city, "Sede A");
        var locB = TestEntities.ClientLocation(clientB, city, "Sede B");
        var brand = TestEntities.AssetBrand("Ricoh");
        var model = TestEntities.AssetModel(brand);
        var assetA = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, locA.Id);
        assetA.ClientId = clientA.Id;
        var assetB = TestEntities.Asset(model, AssetLifecycleStatus.Instalado, locB.Id);
        assetB.ClientId = clientB.Id;
        var location = new InventoryLocation { Kind = InventoryLocationKind.Principal, Name = "Bodega" };
        var toner = new InventoryItem { Name = "Tóner negro", Category = InventoryCategory.Toner };
        var other = new InventoryItem { Name = "Fusor", Category = InventoryCategory.ConsumibleBase };
        db.AddRange(zone, city, clientA, clientB, locA, locB, brand, model, assetA, assetB, location, toner, other);

        InventoryMovement Move(Asset asset, Client client, InventoryItem item, DateTime at, int qty, long? counter, bool delivered = false) => new()
        {
            InventoryItemId = item.Id, InventoryLocationId = location.Id, Type = InventoryMovementType.Consumo, Delta = -qty,
            AssetId = asset.Id, ClientId = client.Id, OccurredAt = at, CounterValue = counter, DeliveredToUser = delivered
        };

        var now = DateTime.UtcNow;
        db.InventoryMovements.AddRange(
            Move(assetA, clientA, toner, now.AddDays(-60), 1, 1000), Move(assetA, clientA, toner, now.AddDays(-30), 1, 4000),
            Move(assetB, clientB, toner, now.AddDays(-50), 1, 0), Move(assetB, clientB, toner, now.AddDays(-20), 2, 2000, delivered: true),
            Move(assetA, clientA, other, now.AddDays(-10), 1, 4500));   // un fusor no es tóner: no entra al BI
        await db.SaveChangesAsync();
        return new Setup(dbName, clientA.Id, clientB.Id);
    }

    private static TonerAnalyticsService Service(Infrastructure.Persistence.TonerDbContext db) => new(db, TimeProvider.System);

    [Fact]
    public async Task Resumen_SumaSoloTonerConAsignacionDeMaquina_YAgrupaPorClienteYZona()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        var summary = await Service(db).GetSummaryAsync(new TonerFilter());

        Assert.Equal(5, summary.TotalUnits);   // 1 + 1 + 1 + 2 de tóner; el fusor no es tóner y no cuenta
        Assert.Equal(2, summary.Machines);
        Assert.Equal(2, summary.DeliveredToUserUnits);
        Assert.Equal(new[] { "Cliente A", "Cliente B" }.OrderBy(x => x), summary.ByClient.Select(c => c.Name).OrderBy(x => x));
        Assert.Equal("Sur", Assert.Single(summary.ByZone).Name);
    }

    [Fact]
    public async Task Filtros_PorCliente_AcotanElResultado()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        var page = await Service(db).ListMachinesAsync(new TonerFilter { ClientId = s.ClientA }, null, null);

        var row = Assert.Single(page.Items);
        Assert.Equal("Cliente A", row.ClientName);
        Assert.Equal(2, row.TotalUnits);
        Assert.Equal(3000, row.AvgPagesPerUnit);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Rango_SinRegistros_DevuelveVacio()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        var summary = await Service(db).GetSummaryAsync(new TonerFilter { From = DateTime.UtcNow.AddDays(-5), To = DateTime.UtcNow });

        Assert.Equal(0, summary.TotalUnits);
        Assert.Empty(summary.Monthly);
    }

    [Fact]
    public async Task Csv_LlevaBOM_SeparadorYProtegeContraFormulas()
    {
        var s = await SeedAsync(clientAName: "=HYPERLINK(\"http://x\")");
        using var db = TonerTestDb.CreateContext(s.DbName);

        var csv = await Service(db).ExportCsvAsync(new TonerFilter());

        Assert.StartsWith("﻿Marca;Modelo", csv);
        Assert.Contains("\"'=HYPERLINK(\"\"http://x\"\")\"", csv);   // la celda no queda como fórmula
        Assert.Equal(3, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);   // encabezado + 2 máquinas
    }
}
