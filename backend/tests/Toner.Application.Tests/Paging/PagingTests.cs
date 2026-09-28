using FluentValidation;
using Toner.Application.Assets;
using Toner.Application.Assets.Dtos;
using Toner.Application.Assignment;
using Toner.Application.Common;
using Toner.Application.Common.Paging;
using Toner.Application.Maintenance;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Paging;

// CODE_QUALITY_AUDIT.md hallazgo #4, Pasos 1 y 2.
public class PagingTests
{
    private static AssetService BuildAssetService(TonerDbContext db) =>
        new(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));

    private static readonly RequestingUser Staff = new(Guid.NewGuid(), RoleNames.Administrador, null, null);

    // ── Offset ───────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Offset_DevuelveLaPaginaPedidaConTotalYHasMore()
    {
        var dbName = await SeedAssetsAsync(count: 7);
        using var db = TonerTestDb.CreateContext(dbName);

        var page1 = await BuildAssetService(db).ListAsync(Staff, page: 1, pageSize: 3);
        Assert.Equal(3, page1.Items.Count);
        Assert.Equal(7, page1.TotalCount);
        Assert.Equal(1, page1.Page);
        Assert.Equal(3, page1.PageSize);
        Assert.True(page1.HasMore);
        Assert.Null(page1.NextCursor); // offset no usa cursor

        var page3 = await BuildAssetService(db).ListAsync(Staff, page: 3, pageSize: 3);
        Assert.Single(page3.Items);          // 7 = 3 + 3 + 1
        Assert.False(page3.HasMore);         // última página
    }

    [Fact]
    public async Task Offset_NoRepiteNiSalteaFilasEntrePaginas()
    {
        var dbName = await SeedAssetsAsync(count: 7);
        using var db = TonerTestDb.CreateContext(dbName);
        var service = BuildAssetService(db);

        var seen = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            seen.AddRange((await service.ListAsync(Staff, page, pageSize: 3)).Items.Select(a => a.Id));
        }

        Assert.Equal(7, seen.Count);
        Assert.Equal(7, seen.Distinct().Count());
    }

    // Caso borde pedido explícitamente: un pageSize por encima del tope se RECORTA, no se obedece.
    [Fact]
    public async Task Offset_PageSizeMayorAlMaximo_SeRecorta()
    {
        var dbName = await SeedAssetsAsync(count: 3);
        using var db = TonerTestDb.CreateContext(dbName);

        var result = await BuildAssetService(db).ListAsync(Staff, page: 1, pageSize: 10_000);

        Assert.Equal(PagingDefaults.MaxPageSize, result.PageSize);
        Assert.Equal(3, result.Items.Count);
    }

    [Theory]
    [InlineData(null, PagingDefaults.DefaultPageSize)] // sin pedir nada -> default de la fase
    [InlineData(0, 1)]                                  // 0 y negativos suben al mínimo de 1
    [InlineData(-5, 1)]
    [InlineData(50, 50)]                                // dentro de rango, se respeta
    [InlineData(999, PagingDefaults.MaxPageSize)]       // por encima del tope, se recorta
    public void ResolvePageSize_RecortaAlRango(int? requested, int expected) =>
        Assert.Equal(expected, PagingDefaults.ResolvePageSize(requested));

    // ── Cursor (keyset) ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Cursor_RecorreTodoElHistorialSinRepetirNiSaltear()
    {
        var (dbName, assetId) = await SeedMeterReadingsAsync(count: 7);
        using var db = TonerTestDb.CreateContext(dbName);
        var service = BuildAssetService(db);

        var seen = new List<Guid>();
        string? cursor = null;
        var pages = 0;

        do
        {
            var page = await service.GetMeterReadingsAsync(assetId, cursor, pageSize: 3);
            seen.AddRange(page.Items.Select(r => r.Id));
            cursor = page.NextCursor;
            Assert.Null(page.TotalCount); // cursor no calcula total
            Assert.Null(page.Page);
            pages++;
        }
        while (cursor is not null && pages < 10);

        Assert.Equal(3, pages);                       // 3 + 3 + 1
        Assert.Equal(7, seen.Count);
        Assert.Equal(7, seen.Distinct().Count());
    }

    [Fact]
    public async Task Cursor_OrdenaPorFechaDescendente()
    {
        var (dbName, assetId) = await SeedMeterReadingsAsync(count: 5);
        using var db = TonerTestDb.CreateContext(dbName);

        var page = await BuildAssetService(db).GetMeterReadingsAsync(assetId, cursor: null, pageSize: 5);

        var dates = page.Items.Select(r => r.ReadingDate).ToList();
        Assert.Equal(dates.OrderByDescending(d => d).ToList(), dates);
    }

    [Fact]
    public async Task Cursor_PageSizeMayorAlMaximo_SeRecorta()
    {
        var (dbName, assetId) = await SeedMeterReadingsAsync(count: 3);
        using var db = TonerTestDb.CreateContext(dbName);

        var page = await BuildAssetService(db).GetMeterReadingsAsync(assetId, cursor: null, pageSize: 10_000);

        Assert.Equal(PagingDefaults.MaxPageSize, page.PageSize);
        Assert.Equal(3, page.Items.Count);
        Assert.False(page.HasMore);
        Assert.Null(page.NextCursor); // sin más páginas no se emite cursor
    }

    // Un cursor corrupto es error del CLIENTE: debe dar 400 (ValidationException), no 500.
    [Fact]
    public async Task Cursor_Invalido_LanzaValidationException()
    {
        var (dbName, assetId) = await SeedMeterReadingsAsync(count: 2);
        using var db = TonerTestDb.CreateContext(dbName);

        await Assert.ThrowsAsync<ValidationException>(() =>
            BuildAssetService(db).GetMeterReadingsAsync(assetId, cursor: "no-es-un-cursor", pageSize: 10));
    }

    [Fact]
    public void Cursor_CodificaYDecodificaIdaYVuelta()
    {
        var timestamp = new DateTime(2026, 8, 18, 15, 4, 5, DateTimeKind.Utc);
        var id = Guid.NewGuid();

        var decoded = PageCursor.Decode(PageCursor.Encode(timestamp, id));

        Assert.NotNull(decoded);
        Assert.Equal(timestamp, decoded!.Value.Timestamp);
        Assert.Equal(id, decoded.Value.Id);
    }

    // Empate exacto de fecha: sin el desempate por Id el cursor saltearía o repetiría filas. Todo lo
    // que se crea en un mismo SaveChanges comparte instante, así que no es un caso hipotético.
    [Fact]
    public async Task Cursor_ConFechasIdenticas_NoRepiteNiSalteaGraciasAlDesempatePorId()
    {
        var dbName = Guid.NewGuid().ToString();
        var sameInstant = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
        Guid assetId;

        using (var arrangeDb = TonerTestDb.CreateContext(dbName))
        {
            var brand = TestEntities.AssetBrand();
            var model = TestEntities.AssetModel(brand);
            var asset = TestEntities.Asset(model);
            assetId = asset.Id;
            arrangeDb.AddRange(brand, model, asset);
            for (var i = 0; i < 6; i++)
            {
                arrangeDb.Add(new MeterReading { AssetId = asset.Id, ReadingDate = sameInstant, CounterValue = 100 + i });
            }
            await arrangeDb.SaveChangesAsync();
        }

        using var db = TonerTestDb.CreateContext(dbName);
        var service = BuildAssetService(db);

        var seen = new List<Guid>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = await service.GetMeterReadingsAsync(assetId, cursor, pageSize: 2);
            seen.AddRange(page.Items.Select(r => r.Id));
            cursor = page.NextCursor;
            pages++;
        }
        while (cursor is not null && pages < 10);

        Assert.Equal(6, seen.Count);
        Assert.Equal(6, seen.Distinct().Count());
    }

    // ── Siembra ──────────────────────────────────────────────────────────────────────────────────
    private static async Task<string> SeedAssetsAsync(int count)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        db.AddRange(brand, model);
        for (var i = 0; i < count; i++)
        {
            var asset = TestEntities.Asset(model);
            asset.SerialNumber = $"SN-{i:D4}";
            db.Add(asset);
        }
        await db.SaveChangesAsync();
        return dbName;
    }

    private static async Task<(string DbName, Guid AssetId)> SeedMeterReadingsAsync(int count)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        db.AddRange(brand, model, asset);

        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < count; i++)
        {
            db.Add(new MeterReading
            {
                AssetId = asset.Id,
                ReadingDate = baseDate.AddDays(i),
                CounterValue = 1000 + (i * 100)
            });
        }

        await db.SaveChangesAsync();
        return (dbName, asset.Id);
    }
}
