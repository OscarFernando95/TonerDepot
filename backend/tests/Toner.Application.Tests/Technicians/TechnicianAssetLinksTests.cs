using Microsoft.EntityFrameworkCore;
using Toner.Application.Assets;
using Toner.Application.Assets.Dtos;
using Toner.Application.Maintenance;
using Toner.Application.Technicians;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Zones;
using Toner.Application.Zones.Dtos;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Technicians;

// Autovínculo técnico-activo por zona (decisión de producto 2026-10-11). Los tres disparadores — asignar zona
// al técnico, agregar municipios a una zona, instalar un activo — comparten un único colaborador
// (TechnicianAssetLinks) y confirman sus vínculos en el MISMO SaveChangesAsync que el cambio que los provoca.
public class TechnicianAssetLinksTests
{
    private sealed class World
    {
        public Zone Zone = new() { Name = "Zona Norte" };
        public City InZone = TestEntities.City("Neiva", "Huila");
        public City OutOfZone = TestEntities.City("Cali", "Valle");
        public Client Client = TestEntities.Client();
        public ClientLocation LocationInZone = null!;
        public ClientLocation LocationOutOfZone = null!;
        public AssetBrand Brand = TestEntities.AssetBrand();
        public AssetModel Model = null!;
        public Role TechRole = TestEntities.Role(RoleNames.Tecnico);
        public User TechUser = null!;
        public Technician Technician = null!;

        public World()
        {
            InZone.ZoneId = Zone.Id;
            LocationInZone = TestEntities.ClientLocation(Client, InZone, "Sede en zona");
            LocationOutOfZone = TestEntities.ClientLocation(Client, OutOfZone, "Sede fuera de zona");
            Model = TestEntities.AssetModel(Brand);
            TechUser = TestEntities.User(TechRole);
            Technician = TestEntities.Technician(TechUser, isActive: true, status: TechnicianStatus.Disponible);
        }

        public async Task SeedAsync(string dbName, bool technicianCoversZone, params object[] extra)
        {
            using var db = TonerTestDb.CreateContext(dbName);
            db.AddRange(Zone, InZone, OutOfZone, Client, LocationInZone, LocationOutOfZone, Brand, Model, TechRole, TechUser, Technician);
            if (technicianCoversZone)
            {
                db.TechnicianZones.Add(new TechnicianZone { TechnicianId = Technician.Id, ZoneId = Zone.Id });
            }
            db.AddRange(extra);
            await db.SaveChangesAsync();
        }
    }

    private static TechnicianService TechnicianService(TonerDbContext db) =>
        new(db, TestCalendar.For(db), FixedTimeProvider.WorkingHours);

    private static AssetService AssetService(TonerDbContext db) =>
        new(db, new MaintenanceScheduleEngine(db), TestAssignment.Create(db));

    // Cuenta los SaveChangesAsync que el contexto efectivamente confirmó durante la operación.
    private static Func<int> CountSaves(TonerDbContext db)
    {
        var saves = 0;
        db.SavedChanges += (_, _) => saves++;
        return () => saves;
    }

    [Fact]
    public async Task SetZones_AssigningAZone_LinksOnlyInstalledAssetsOfItsCities_AsZoneSource()
    {
        var dbName = Guid.NewGuid().ToString();
        var w = new World();
        var installedInZone = TestEntities.Asset(w.Model, AssetLifecycleStatus.Instalado, w.LocationInZone.Id);
        var installedOutside = TestEntities.Asset(w.Model, AssetLifecycleStatus.Instalado, w.LocationOutOfZone.Id);
        var inBodega = TestEntities.Asset(w.Model, AssetLifecycleStatus.EnBodega);
        var underMaintenance = TestEntities.Asset(w.Model, AssetLifecycleStatus.EnMantenimiento, w.LocationInZone.Id);
        await w.SeedAsync(dbName, technicianCoversZone: false, installedInZone, installedOutside, inBodega, underMaintenance);

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            var saves = CountSaves(db);
            await TechnicianService(db).SetZonesAsync(w.Technician.Id, new SetTechnicianZonesRequest { ZoneIds = new[] { w.Zone.Id } });
            Assert.Equal(1, saves());
        }

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var link = await assertDb.TechnicianAssets.SingleAsync();
        Assert.Equal(installedInZone.Id, link.AssetId);
        Assert.Equal(w.Technician.Id, link.TechnicianId);
        Assert.Equal(TechnicianAssetSource.Zone, link.Source);
    }

    [Fact]
    public async Task SetZones_RepeatedOrWithExistingManualLink_DoesNotDuplicateNorDowngradeIt()
    {
        var dbName = Guid.NewGuid().ToString();
        var w = new World();
        var asset = TestEntities.Asset(w.Model, AssetLifecycleStatus.Instalado, w.LocationInZone.Id);
        var manual = new TechnicianAsset { TechnicianId = w.Technician.Id, AssetId = asset.Id, Source = TechnicianAssetSource.Manual };
        await w.SeedAsync(dbName, technicianCoversZone: false, asset, manual);

        for (var i = 0; i < 2; i++)
        {
            using var db = TonerTestDb.CreateContext(dbName);
            await TechnicianService(db).SetZonesAsync(w.Technician.Id, new SetTechnicianZonesRequest { ZoneIds = new[] { w.Zone.Id } });
        }

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var link = await assertDb.TechnicianAssets.SingleAsync();
        Assert.Equal(manual.Id, link.Id);
        Assert.Equal(TechnicianAssetSource.Manual, link.Source);
    }

    [Fact]
    public async Task SetCities_AddingACityToAZoneWithTechnician_LinksItsInstalledAssets_InOneSave()
    {
        var dbName = Guid.NewGuid().ToString();
        var w = new World();
        var cityOutside = w.OutOfZone;
        var assetInCityToAdd = TestEntities.Asset(w.Model, AssetLifecycleStatus.Instalado, w.LocationOutOfZone.Id);
        var assetAlreadyInZone = TestEntities.Asset(w.Model, AssetLifecycleStatus.Instalado, w.LocationInZone.Id);
        await w.SeedAsync(dbName, technicianCoversZone: true, assetInCityToAdd, assetAlreadyInZone);

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            var saves = CountSaves(db);
            await new ZoneService(db).SetCitiesAsync(
                w.Zone.Id, new SetZoneCitiesRequest { CityIds = new[] { w.InZone.Id, cityOutside.Id } });
            Assert.Equal(1, saves());
        }

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var linkedAssetIds = await assertDb.TechnicianAssets.Select(ta => ta.AssetId).ToListAsync();
        // El activo de un municipio que ya estaba en la zona también se vincula: la zona ya tenía técnico y
        // nadie lo había vinculado todavía.
        Assert.Contains(assetInCityToAdd.Id, linkedAssetIds);
        Assert.Contains(assetAlreadyInZone.Id, linkedAssetIds);
        Assert.Equal(2, linkedAssetIds.Count);
        Assert.Equal(w.Zone.Id, (await assertDb.Cities.SingleAsync(c => c.Id == cityOutside.Id)).ZoneId);
    }

    [Fact]
    public async Task ChangeStatus_ToInstalado_LinksTheTechniciansOfTheZone_InOneSave()
    {
        var dbName = Guid.NewGuid().ToString();
        var w = new World();
        var asset = TestEntities.Asset(w.Model, AssetLifecycleStatus.PendienteInstalacion, w.LocationInZone.Id);
        await w.SeedAsync(dbName, technicianCoversZone: true, asset);

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            var saves = CountSaves(db);
            await AssetService(db).ChangeStatusAsync(
                asset.Id,
                new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.Instalado), Area = "Recepción" },
                Guid.NewGuid());
            Assert.Equal(1, saves());
        }

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var link = await assertDb.TechnicianAssets.SingleAsync();
        Assert.Equal(asset.Id, link.AssetId);
        Assert.Equal(w.Technician.Id, link.TechnicianId);
        Assert.Equal(TechnicianAssetSource.Zone, link.Source);
    }

    [Fact]
    public async Task CheckOut_InstallationByATechnicianThatCoversTheZone_LeavesExactlyOneLink()
    {
        var dbName = Guid.NewGuid().ToString();
        var w = new World();
        var asset = TestEntities.Asset(w.Model, AssetLifecycleStatus.PendienteInstalacion, w.LocationInZone.Id);
        await w.SeedAsync(dbName, technicianCoversZone: true, asset);

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            await TestCheckIn.Create(db).CheckInAsync(w.Technician.Id, new CheckInRequest { AssetId = asset.Id });
        }

        // Regresión del índice único TechnicianId+AssetId: el sync por zona y el vínculo del instalador caen en
        // la misma operación y el segundo no debe duplicar al primero.
        using (var db = TonerTestDb.CreateContext(dbName))
        {
            await TestCheckIn.Create(db).CheckOutAsync(w.Technician.Id, new CheckOutRequest
            {
                Resolved = true,
                Area = "Recepción",
                InitialCounterValue = 1200,
                GeneralMaintenanceDone = true,
                UnitsMaintenanceDone = false
            });
        }

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var link = await assertDb.TechnicianAssets.SingleAsync(ta => ta.AssetId == asset.Id);
        Assert.Equal(w.Technician.Id, link.TechnicianId);
        // Quien instaló el equipo lo conserva aunque luego pierda la zona: el sync por zona lo había dejado
        // como Zone y la instalación confirmada lo vuelve explícito.
        Assert.Equal(TechnicianAssetSource.Manual, link.Source);
    }

    // ── Revocación ───────────────────────────────────────────────────────────────────────────────────────
    // TechnicianAsset concede permisos (ver y registrar tóner): lo automático se retira cuando deja de aplicar,
    // y lo manual nunca se retira por cambios de cobertura.

    [Fact]
    public async Task SetZones_RemovingTheZone_RevokesZoneLinks_ButKeepsManualOnes()
    {
        var dbName = Guid.NewGuid().ToString();
        var w = new World();
        var byZone = TestEntities.Asset(w.Model, AssetLifecycleStatus.Instalado, w.LocationInZone.Id);
        var byHand = TestEntities.Asset(w.Model, AssetLifecycleStatus.Instalado, w.LocationInZone.Id);
        var outsideByHand = TestEntities.Asset(w.Model, AssetLifecycleStatus.Instalado, w.LocationOutOfZone.Id);
        await w.SeedAsync(dbName, technicianCoversZone: true, byZone, byHand, outsideByHand,
            new TechnicianAsset { TechnicianId = w.Technician.Id, AssetId = byZone.Id, Source = TechnicianAssetSource.Zone },
            new TechnicianAsset { TechnicianId = w.Technician.Id, AssetId = byHand.Id, Source = TechnicianAssetSource.Manual },
            new TechnicianAsset { TechnicianId = w.Technician.Id, AssetId = outsideByHand.Id, Source = TechnicianAssetSource.Manual });

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            await TechnicianService(db).SetZonesAsync(w.Technician.Id, new SetTechnicianZonesRequest());
        }

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var remaining = await assertDb.TechnicianAssets.Select(ta => ta.AssetId).ToListAsync();
        Assert.DoesNotContain(byZone.Id, remaining);
        Assert.Contains(byHand.Id, remaining);
        Assert.Contains(outsideByHand.Id, remaining);
    }

    [Fact]
    public async Task SetCities_RemovingACityFromTheZone_RevokesTheZoneLinksOfItsAssets()
    {
        var dbName = Guid.NewGuid().ToString();
        var w = new World();
        var asset = TestEntities.Asset(w.Model, AssetLifecycleStatus.Instalado, w.LocationInZone.Id);
        await w.SeedAsync(dbName, technicianCoversZone: true, asset,
            new TechnicianAsset { TechnicianId = w.Technician.Id, AssetId = asset.Id, Source = TechnicianAssetSource.Zone });

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            var saves = CountSaves(db);
            await new ZoneService(db).SetCitiesAsync(w.Zone.Id, new SetZoneCitiesRequest());
            Assert.Equal(1, saves());
        }

        using var assertDb = TonerTestDb.CreateContext(dbName);
        Assert.Empty(assertDb.TechnicianAssets);
        Assert.Null((await assertDb.Cities.SingleAsync(c => c.Id == w.InZone.Id)).ZoneId);
    }

    [Fact]
    public async Task SetCities_MovingACityToAnotherZone_RevokesFromTheOldTechniciansAndLinksTheNewOnes()
    {
        var dbName = Guid.NewGuid().ToString();
        var w = new World();
        var otherZone = new Zone { Name = "Zona Sur" };
        var otherRole = TestEntities.Role(RoleNames.Tecnico);
        var otherUser = TestEntities.User(otherRole);
        var otherTechnician = TestEntities.Technician(otherUser, isActive: true, status: TechnicianStatus.Disponible);
        var asset = TestEntities.Asset(w.Model, AssetLifecycleStatus.Instalado, w.LocationInZone.Id);
        await w.SeedAsync(dbName, technicianCoversZone: true, asset, otherZone, otherRole, otherUser, otherTechnician,
            new TechnicianZone { TechnicianId = otherTechnician.Id, ZoneId = otherZone.Id },
            new TechnicianAsset { TechnicianId = w.Technician.Id, AssetId = asset.Id, Source = TechnicianAssetSource.Zone });

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            // Neiva pasa de Norte a Sur.
            await new ZoneService(db).SetCitiesAsync(otherZone.Id, new SetZoneCitiesRequest { CityIds = new[] { w.InZone.Id } });
        }

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var link = await assertDb.TechnicianAssets.SingleAsync();
        Assert.Equal(otherTechnician.Id, link.TechnicianId);
        Assert.Equal(TechnicianAssetSource.Zone, link.Source);
    }

    [Fact]
    public async Task ChangeStatus_InstalledAssetBackToBodega_RevokesZoneLinks_ButKeepsManualOnes()
    {
        var dbName = Guid.NewGuid().ToString();
        var w = new World();
        var asset = TestEntities.Asset(w.Model, AssetLifecycleStatus.Instalado, w.LocationInZone.Id);
        var otherRole = TestEntities.Role(RoleNames.Tecnico);
        var otherUser = TestEntities.User(otherRole);
        var manualTechnician = TestEntities.Technician(otherUser, isActive: true, status: TechnicianStatus.Disponible);
        await w.SeedAsync(dbName, technicianCoversZone: true, asset, otherRole, otherUser, manualTechnician,
            new TechnicianAsset { TechnicianId = w.Technician.Id, AssetId = asset.Id, Source = TechnicianAssetSource.Zone },
            new TechnicianAsset { TechnicianId = manualTechnician.Id, AssetId = asset.Id, Source = TechnicianAssetSource.Manual });

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            await AssetService(db).ChangeStatusAsync(
                asset.Id, new ChangeAssetStatusRequest { NewStatus = nameof(AssetLifecycleStatus.EnBodega) }, Guid.NewGuid());
        }

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var link = await assertDb.TechnicianAssets.SingleAsync();
        Assert.Equal(manualTechnician.Id, link.TechnicianId);
    }

    [Fact]
    public async Task LinkAsset_OverAZoneLink_PromotesItToManual_SoItSurvivesLosingTheZone()
    {
        var dbName = Guid.NewGuid().ToString();
        var w = new World();
        var asset = TestEntities.Asset(w.Model, AssetLifecycleStatus.Instalado, w.LocationInZone.Id);
        await w.SeedAsync(dbName, technicianCoversZone: true, asset,
            new TechnicianAsset { TechnicianId = w.Technician.Id, AssetId = asset.Id, Source = TechnicianAssetSource.Zone });

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            var dto = await TechnicianService(db).LinkAssetAsync(w.Technician.Id, new AddTechnicianAssetRequest { AssetId = asset.Id });
            Assert.Equal(nameof(TechnicianAssetSource.Manual), dto.Source);
        }

        using (var db = TonerTestDb.CreateContext(dbName))
        {
            await TechnicianService(db).SetZonesAsync(w.Technician.Id, new SetTechnicianZonesRequest());
        }

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var link = await assertDb.TechnicianAssets.SingleAsync();
        Assert.Equal(TechnicianAssetSource.Manual, link.Source);
    }

    [Fact]
    public async Task UnlinkAsset_ZoneLink_IsRejected_ManualLinkIsRemoved()
    {
        var dbName = Guid.NewGuid().ToString();
        var w = new World();
        var byZone = TestEntities.Asset(w.Model, AssetLifecycleStatus.Instalado, w.LocationInZone.Id);
        var byHand = TestEntities.Asset(w.Model, AssetLifecycleStatus.Instalado, w.LocationOutOfZone.Id);
        var zoneLink = new TechnicianAsset { TechnicianId = w.Technician.Id, AssetId = byZone.Id, Source = TechnicianAssetSource.Zone };
        var manualLink = new TechnicianAsset { TechnicianId = w.Technician.Id, AssetId = byHand.Id, Source = TechnicianAssetSource.Manual };
        await w.SeedAsync(dbName, technicianCoversZone: true, byZone, byHand, zoneLink, manualLink);

        using var db = TonerTestDb.CreateContext(dbName);
        var service = TechnicianService(db);

        await Assert.ThrowsAsync<Common.Exceptions.ConflictException>(() => service.UnlinkAssetAsync(w.Technician.Id, zoneLink.Id));
        await service.UnlinkAssetAsync(w.Technician.Id, manualLink.Id);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        Assert.Equal(zoneLink.Id, (await assertDb.TechnicianAssets.SingleAsync()).Id);
    }
}
