using Microsoft.EntityFrameworkCore;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Technicians;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Technicians.Validators;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Zones;
using Toner.Application.Zones.Dtos;
using Toner.Application.Zones.Validators;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Zones;

// Zonas: agrupan municipios (cada municipio en una sola zona) y se asignan a los técnicos; la cobertura por
// municipio de un técnico se deriva de sus zonas.
public class ZoneServiceTests
{
    private static ZoneService Service(Infrastructure.Persistence.TonerDbContext db) => new(db);

    [Fact]
    public async Task Crear_RecortaElNombre_YRechazaUnoRepetidoSinImportarMayusculas()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);

        var zone = await Service(db).CreateAsync(new CreateZoneRequest { Name = "  Zona Huila  " });

        Assert.Equal("Zona Huila", zone.Name);
        await Assert.ThrowsAsync<ConflictException>(() => Service(db).CreateAsync(new CreateZoneRequest { Name = "zona huila" }));
    }

    [Fact]
    public async Task Renombrar_ConElNombreDeOtraZona_SeRechaza_PeroConElPropioSePermite()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var a = await Service(db).CreateAsync(new CreateZoneRequest { Name = "A" });
        await Service(db).CreateAsync(new CreateZoneRequest { Name = "B" });

        await Assert.ThrowsAsync<ConflictException>(() => Service(db).RenameAsync(a.Id, new UpdateZoneRequest { Name = "B" }));
        var same = await Service(db).RenameAsync(a.Id, new UpdateZoneRequest { Name = "A" });
        Assert.Equal("A", same.Name);
    }

    [Fact]
    public async Task AsignarMunicipios_LosMueveDesdeOtraZona_YLiberaLosQueYaNoEstan()
    {
        var dbName = Guid.NewGuid().ToString();
        var neiva = TestEntities.City("Neiva", "Huila");
        var pitalito = TestEntities.City("Pitalito", "Huila");
        var garzon = TestEntities.City("Garzón", "Huila");
        using (var arrange = TonerTestDb.CreateContext(dbName))
        {
            arrange.AddRange(neiva, pitalito, garzon);
            await arrange.SaveChangesAsync();
        }

        Guid northId, southId;
        using (var db = TonerTestDb.CreateContext(dbName))
        {
            northId = (await Service(db).CreateAsync(new CreateZoneRequest { Name = "Norte" })).Id;
            southId = (await Service(db).CreateAsync(new CreateZoneRequest { Name = "Sur" })).Id;
            await Service(db).SetCitiesAsync(northId, new SetZoneCitiesRequest { CityIds = new[] { neiva.Id, pitalito.Id } });
        }

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            // Sur toma a Pitalito (estaba en Norte) y a Garzón (sin zona).
            var south = await Service(db).SetCitiesAsync(southId, new SetZoneCitiesRequest { CityIds = new[] { pitalito.Id, garzon.Id } });
            Assert.Equal(new[] { "Garzón", "Pitalito" }, south.Cities.Select(c => c.Name));
        }

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            var zones = await Service(db).ListAsync();
            Assert.Equal(new[] { "Neiva" }, zones.Single(z => z.Id == northId).Cities.Select(c => c.Name));

            // Y ahora Sur suelta a Garzón: queda sin zona.
            await Service(db).SetCitiesAsync(southId, new SetZoneCitiesRequest { CityIds = new[] { pitalito.Id } });
            Assert.Null((await db.Cities.SingleAsync(c => c.Id == garzon.Id)).ZoneId);
        }
    }

    [Fact]
    public async Task Eliminar_ConTecnicosAsignados_SeRechaza_YSinTecnicosLiberaSusMunicipios()
    {
        var dbName = Guid.NewGuid().ToString();
        var city = TestEntities.City();
        var role = TestEntities.Role(RoleNames.Tecnico);
        var user = TestEntities.User(role);
        var technician = TestEntities.Technician(user, isActive: true, status: TechnicianStatus.Disponible);
        using (var arrange = TonerTestDb.CreateContext(dbName))
        {
            arrange.AddRange(city, role, user, technician);
            await arrange.SaveChangesAsync();
        }

        Guid zoneId;
        using (var db = TonerTestDb.CreateContext(dbName))
        {
            zoneId = (await Service(db).CreateAsync(new CreateZoneRequest { Name = "Z" })).Id;
            await Service(db).SetCitiesAsync(zoneId, new SetZoneCitiesRequest { CityIds = new[] { city.Id } });
            db.TechnicianZones.Add(new TechnicianZone { TechnicianId = technician.Id, ZoneId = zoneId });
            await db.SaveChangesAsync();
        }

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            await Assert.ThrowsAsync<ConflictException>(() => Service(db).DeleteAsync(zoneId));
            db.TechnicianZones.RemoveRange(db.TechnicianZones);
            await db.SaveChangesAsync();
            await Service(db).DeleteAsync(zoneId);
        }

        using var check = TonerTestDb.CreateContext(dbName);
        Assert.Empty(check.Zones);
        Assert.Null((await check.Cities.SingleAsync()).ZoneId);
    }

    [Fact]
    public async Task ZonaInexistente_DaNotFound()
    {
        using var db = TonerTestDb.CreateContext(Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).RenameAsync(Guid.NewGuid(), new UpdateZoneRequest { Name = "X" }));
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).SetCitiesAsync(Guid.NewGuid(), new SetZoneCitiesRequest()));
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ValidadorDeMunicipios_RechazaRepetidosEInexistentes()
    {
        var dbName = Guid.NewGuid().ToString();
        var city = TestEntities.City();
        using (var arrange = TonerTestDb.CreateContext(dbName))
        {
            arrange.Add(city);
            await arrange.SaveChangesAsync();
        }

        using var db = TonerTestDb.CreateContext(dbName);
        var validator = new SetZoneCitiesRequestValidator(db);

        Assert.True((await validator.ValidateAsync(new SetZoneCitiesRequest { CityIds = new[] { city.Id } })).IsValid);
        Assert.False((await validator.ValidateAsync(new SetZoneCitiesRequest { CityIds = new[] { city.Id, city.Id } })).IsValid);
        Assert.False((await validator.ValidateAsync(new SetZoneCitiesRequest { CityIds = new[] { Guid.NewGuid() } })).IsValid);
    }

    [Fact]
    public async Task ZonasDelTecnico_SeReemplazanComoConjunto_YDeterminanSuCobertura()
    {
        var dbName = Guid.NewGuid().ToString();
        var role = TestEntities.Role(RoleNames.Tecnico);
        var user = TestEntities.User(role);
        var technician = TestEntities.Technician(user, isActive: true, status: TechnicianStatus.Disponible);
        var neiva = TestEntities.City("Neiva", "Huila");
        var pitalito = TestEntities.City("Pitalito", "Huila");
        using (var arrange = TonerTestDb.CreateContext(dbName))
        {
            arrange.AddRange(role, user, technician, neiva, pitalito);
            await arrange.SaveChangesAsync();
        }

        Guid northId, southId;
        using (var db = TonerTestDb.CreateContext(dbName))
        {
            northId = (await Service(db).CreateAsync(new CreateZoneRequest { Name = "Norte" })).Id;
            southId = (await Service(db).CreateAsync(new CreateZoneRequest { Name = "Sur" })).Id;
            await Service(db).SetCitiesAsync(northId, new SetZoneCitiesRequest { CityIds = new[] { neiva.Id } });
            await Service(db).SetCitiesAsync(southId, new SetZoneCitiesRequest { CityIds = new[] { pitalito.Id } });
        }

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            var technicians = TestTechnicianService(db);
            var zones = await technicians.SetZonesAsync(technician.Id, new SetTechnicianZonesRequest { ZoneIds = new[] { northId } });
            Assert.Equal(new[] { "Norte" }, zones.Select(z => z.ZoneName));
            Assert.Equal(new[] { neiva.Id }, await db.CoveredCityIds(technician.Id).ToListAsync());
        }

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            // Pasa de Norte a Sur: la cobertura cambia de Neiva a Pitalito.
            await TestTechnicianService(db).SetZonesAsync(technician.Id, new SetTechnicianZonesRequest { ZoneIds = new[] { southId } });
            Assert.Equal(new[] { pitalito.Id }, await db.CoveredCityIds(technician.Id).ToListAsync());
        }

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            // Mover Neiva a Sur amplía la cobertura de quien atiende Sur, sin tocar al técnico.
            await Service(db).SetCitiesAsync(southId, new SetZoneCitiesRequest { CityIds = new[] { pitalito.Id, neiva.Id } });
            Assert.Equivalent(new[] { neiva.Id, pitalito.Id }, await db.CoveredCityIds(technician.Id).ToListAsync(), strict: true);

            await TestTechnicianService(db).SetZonesAsync(technician.Id, new SetTechnicianZonesRequest());
            Assert.Empty(await db.CoveredCityIds(technician.Id).ToListAsync());
        }
    }

    [Fact]
    public async Task ValidadorDeZonasDelTecnico_RechazaZonasInexistentes()
    {
        using var db = TonerTestDb.CreateContext(Guid.NewGuid().ToString());
        var validator = new SetTechnicianZonesRequestValidator(db);

        Assert.False((await validator.ValidateAsync(new SetTechnicianZonesRequest { ZoneIds = new[] { Guid.NewGuid() } })).IsValid);
        Assert.True((await validator.ValidateAsync(new SetTechnicianZonesRequest())).IsValid);
    }

    private static TechnicianService TestTechnicianService(Infrastructure.Persistence.TonerDbContext db) =>
        new(db, TestCalendar.For(db), FixedTimeProvider.WorkingHours);
}
