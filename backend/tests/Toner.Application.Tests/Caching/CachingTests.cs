using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Toner.Application.Assets;
using Toner.Application.Assets.Dtos;
using Toner.Application.Cities;
using Toner.Application.Common.Caching;
using Toner.Application.Dashboard;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Entities;

namespace Toner.Application.Tests.Caching;

// CODE_QUALITY_AUDIT.md hallazgo #10.
//
// Cómo se prueba que la segunda llamada NO toca la base, sin mocks: se llama, se BORRAN los datos
// por fuera (otro contexto sobre la misma base InMemory), y se vuelve a llamar. Si sigue devolviendo
// lo viejo, la respuesta salió de la caché — no hay forma de que venga de la base. Después se
// invalida o se expira y se confirma que ahí sí refresca.
//
// Un mock de IMemoryCache probaría el mock; una caché real prueba el comportamiento.
public class CachingTests
{
    // ── Ciudades ─────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Cities_SegundaLlamada_NoConsultaLaBase()
    {
        var dbName = await SeedCitiesAsync(3);
        var cache = TestCache.New();

        using var db1 = TonerTestDb.CreateContext(dbName);
        var first = await new CityService(db1, cache).ListAsync();
        Assert.Equal(3, first.Count);

        await DeleteAllCitiesAsync(dbName);

        // Contexto NUEVO y caché compartida: si consultara la base, vería 0 filas.
        using var db2 = TonerTestDb.CreateContext(dbName);
        var second = await new CityService(db2, cache).ListAsync();
        Assert.Equal(3, second.Count);

        // Con caché nueva sí ve la base vacía — confirma que el 3 anterior venía de la caché y no de
        // datos que hubieran quedado por ahí.
        using var db3 = TonerTestDb.CreateContext(dbName);
        Assert.Empty(await new CityService(db3, TestCache.New()).ListAsync());
    }

    [Fact]
    public async Task Cities_AlExpirarElTtl_VuelveAConsultarLaBase()
    {
        var dbName = await SeedCitiesAsync(3);
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var cache = new MemoryCache(new MemoryCacheOptions { Clock = clock });

        using var db1 = TonerTestDb.CreateContext(dbName);
        Assert.Equal(3, (await new CityService(db1, cache).ListAsync()).Count);

        await DeleteAllCitiesAsync(dbName);

        // Justo antes de vencer: sigue sirviendo de caché.
        clock.Advance(CacheDurations.Catalog - TimeSpan.FromMinutes(1));
        using var db2 = TonerTestDb.CreateContext(dbName);
        Assert.Equal(3, (await new CityService(db2, cache).ListAsync()).Count);

        // Pasado el TTL: la entrada venció y vuelve a la base, que ahora está vacía.
        clock.Advance(TimeSpan.FromMinutes(2));
        using var db3 = TonerTestDb.CreateContext(dbName);
        Assert.Empty(await new CityService(db3, cache).ListAsync());
    }

    // ── Marcas ───────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task AssetBrands_SegundaLlamada_NoConsultaLaBase()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seed = TonerTestDb.CreateContext(dbName))
        {
            seed.AddRange(TestEntities.AssetBrand("Ricoh"), TestEntities.AssetBrand("Kyocera"));
            await seed.SaveChangesAsync();
        }
        var cache = TestCache.New();

        using var db1 = TonerTestDb.CreateContext(dbName);
        Assert.Equal(2, (await new AssetBrandService(db1, cache).ListAsync()).Count);

        using (var wipe = TonerTestDb.CreateContext(dbName))
        {
            wipe.AssetBrands.RemoveRange(await wipe.AssetBrands.ToListAsync());
            await wipe.SaveChangesAsync();
        }

        using var db2 = TonerTestDb.CreateContext(dbName);
        Assert.Equal(2, (await new AssetBrandService(db2, cache).ListAsync()).Count);
    }

    [Fact]
    public async Task AssetBrands_CrearMarca_InvalidaLaCache()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seed = TonerTestDb.CreateContext(dbName))
        {
            seed.Add(TestEntities.AssetBrand("Ricoh"));
            await seed.SaveChangesAsync();
        }
        var cache = TestCache.New();

        using var db1 = TonerTestDb.CreateContext(dbName);
        Assert.Single(await new AssetBrandService(db1, cache).ListAsync());

        using var db2 = TonerTestDb.CreateContext(dbName);
        await new AssetBrandService(db2, cache).CreateAsync(new CreateAssetBrandRequest { Name = "Kyocera" });

        // Sin invalidación activa esto seguiría devolviendo 1 durante 4 horas.
        using var db3 = TonerTestDb.CreateContext(dbName);
        Assert.Equal(2, (await new AssetBrandService(db3, cache).ListAsync()).Count);
    }

    // ── Modelos (una entrada por marca) ──────────────────────────────────────────────────────────
    [Fact]
    public async Task AssetModels_CrearModelo_InvalidaSoloLaMarcaAfectada()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid brandA, brandB;
        using (var seed = TonerTestDb.CreateContext(dbName))
        {
            var a = TestEntities.AssetBrand("MarcaA");
            var b = TestEntities.AssetBrand("MarcaB");
            brandA = a.Id; brandB = b.Id;
            seed.AddRange(a, b, TestEntities.AssetModel(a, "M-A1"), TestEntities.AssetModel(b, "M-B1"));
            await seed.SaveChangesAsync();
        }
        var cache = TestCache.New();

        // Ambas marcas quedan cacheadas.
        using (var db = TonerTestDb.CreateContext(dbName))
        {
            var service = new AssetModelService(db, cache);
            Assert.Single(await service.ListAsync(brandA));
            Assert.Single(await service.ListAsync(brandB));
        }

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            await new AssetModelService(db, cache).CreateAsync(brandA, NewModelRequest("M-A2"));
        }

        // Se borran los modelos de B por fuera: si su caché hubiera sido invalidada de rebote, este
        // listado devolvería 0 en vez de 1.
        using (var wipe = TonerTestDb.CreateContext(dbName))
        {
            wipe.AssetModels.RemoveRange(await wipe.AssetModels.Where(m => m.AssetBrandId == brandB).ToListAsync());
            await wipe.SaveChangesAsync();
        }

        using var db2 = TonerTestDb.CreateContext(dbName);
        var service2 = new AssetModelService(db2, cache);
        Assert.Equal(2, (await service2.ListAsync(brandA)).Count); // A refrescó
        Assert.Single(await service2.ListAsync(brandB));           // B sigue en caché, aislada
    }

    [Fact]
    public async Task AssetModels_EditarModelo_InvalidaLaCache()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid brandId, modelId;
        using (var seed = TonerTestDb.CreateContext(dbName))
        {
            var brand = TestEntities.AssetBrand("Ricoh");
            var model = TestEntities.AssetModel(brand, "MP2014");
            brandId = brand.Id; modelId = model.Id;
            seed.AddRange(brand, model);
            await seed.SaveChangesAsync();
        }
        var cache = TestCache.New();

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            Assert.Equal("MP2014", (await new AssetModelService(db, cache).ListAsync(brandId))[0].Name);
        }

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            await new AssetModelService(db, cache).UpdateAsync(brandId, modelId, new UpdateAssetModelRequest
            {
                Name = "MP2015",
                GeneralPrintThreshold = 30000, GeneralMonthsInterval = 6,
                UnitsPrintThreshold = 30000, UnitsMonthsInterval = 6,
                ConsumablesPrintThreshold = 60000
            });
        }

        using var db2 = TonerTestDb.CreateContext(dbName);
        Assert.Equal("MP2015", (await new AssetModelService(db2, cache).ListAsync(brandId))[0].Name);
    }

    // ── Dashboard ────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Dashboard_SegundaLlamada_NoConsultaLaBase()
    {
        var dbName = await SeedResolvedTicketAsync();
        var cache = TestCache.New();

        using var db1 = TonerTestDb.CreateContext(dbName);
        var first = await new DashboardService(db1, cache, TestCache.StaffTenant(), TestCalendar.For(db1)).GetSummaryAsync(30);
        Assert.Equal(1, first.Mttr.ResolvedTicketCount);

        using (var wipe = TonerTestDb.CreateContext(dbName))
        {
            wipe.ServiceTickets.RemoveRange(await wipe.ServiceTickets.ToListAsync());
            await wipe.SaveChangesAsync();
        }

        using var db2 = TonerTestDb.CreateContext(dbName);
        var second = await new DashboardService(db2, cache, TestCache.StaffTenant(), TestCalendar.For(db2)).GetSummaryAsync(30);
        Assert.Equal(1, second.Mttr.ResolvedTicketCount); // de la caché
    }

    [Fact]
    public async Task Dashboard_PeriodDaysNoValido_SeNormalizaYComparteEntradaCon30()
    {
        var dbName = await SeedResolvedTicketAsync();
        var cache = TestCache.New();

        using var db1 = TonerTestDb.CreateContext(dbName);
        await new DashboardService(db1, cache, TestCache.StaffTenant(), TestCalendar.For(db1)).GetSummaryAsync(0); // -> 30

        await using (var wipe = TonerTestDb.CreateContext(dbName))
        {
            wipe.ServiceTickets.RemoveRange(await wipe.ServiceTickets.ToListAsync());
            await wipe.SaveChangesAsync();
        }

        // 0, -5 y 30 deben caer en la MISMA entrada: si no, cada variante recalcularía y vería la
        // base ya vacía.
        using var db2 = TonerTestDb.CreateContext(dbName);
        var service = new DashboardService(db2, cache, TestCache.StaffTenant(), TestCalendar.For(db2));
        Assert.Equal(1, (await service.GetSummaryAsync(-5)).Mttr.ResolvedTicketCount);
        Assert.Equal(1, (await service.GetSummaryAsync(30)).Mttr.ResolvedTicketCount);
    }

    // La propiedad de seguridad del hallazgo: staff y cliente NO comparten entrada de caché.
    [Fact]
    public async Task Dashboard_StaffYCliente_NoCompartenEntradaDeCache()
    {
        var dbName = await SeedResolvedTicketAsync();
        var cache = TestCache.New();

        using var db1 = TonerTestDb.CreateContext(dbName);
        Assert.Equal(1, (await new DashboardService(db1, cache, TestCache.StaffTenant(), TestCalendar.For(db1)).GetSummaryAsync(30)).Mttr.ResolvedTicketCount);

        using (var wipe = TonerTestDb.CreateContext(dbName))
        {
            wipe.ServiceTickets.RemoveRange(await wipe.ServiceTickets.ToListAsync());
            await wipe.SaveChangesAsync();
        }

        // Un Cliente con la MISMA caché no debe leer la entrada del staff: al no encontrar la suya,
        // recalcula y ve la base (vacía). Si compartieran clave, devolvería el 1 del staff — que es
        // justamente la fuga entre tenants que la clave con tenant previene.
        using var db2 = TonerTestDb.CreateContext(dbName);
        var asClient = new DashboardService(db2, cache, TestCache.ClientTenant(Guid.NewGuid()), TestCalendar.For(db2));
        Assert.Equal(0, (await asClient.GetSummaryAsync(30)).Mttr.ResolvedTicketCount);
    }

    // ── Utilidades ───────────────────────────────────────────────────────────────────────────────
    private static CreateAssetModelRequest NewModelRequest(string name) => new()
    {
        Name = name,
        GeneralPrintThreshold = 30000, GeneralMonthsInterval = 6,
        UnitsPrintThreshold = 30000, UnitsMonthsInterval = 6,
        ConsumablesPrintThreshold = 60000
    };

    private static async Task<string> SeedCitiesAsync(int count)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        for (var i = 0; i < count; i++)
        {
            db.Add(TestEntities.City($"Ciudad {i}", "Depto"));
        }
        await db.SaveChangesAsync();
        return dbName;
    }

    private static async Task DeleteAllCitiesAsync(string dbName)
    {
        using var db = TonerTestDb.CreateContext(dbName);
        db.Cities.RemoveRange(await db.Cities.ToListAsync());
        await db.SaveChangesAsync();
    }

    private static async Task<string> SeedResolvedTicketAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(Domain.Common.RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var ticket = TestEntities.ServiceTicket(location, user, Domain.Enums.ServiceTicketStatus.Resuelto);
        ticket.ResolvedAt = DateTime.UtcNow.AddHours(-2);
        db.AddRange(role, user, city, client, location, ticket);
        await db.SaveChangesAsync();
        return dbName;
    }

    // Reloj falso para probar el TTL sin esperar 4 horas. MemoryCache no desaloja de forma proactiva:
    // comprueba el vencimiento al acceder, así que adelantar el reloj y volver a pedir la entrada es
    // suficiente para observar la expiración.
    private sealed class FakeClock : ISystemClock
    {
        public FakeClock(DateTimeOffset start) => UtcNow = start;

        public DateTimeOffset UtcNow { get; private set; }

        public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
    }
}
