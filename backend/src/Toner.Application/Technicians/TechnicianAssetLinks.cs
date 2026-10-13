using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Technicians;

// Único dueño de la regla "los activos Instalado de la zona de un técnico quedan vinculados a él" (decisión de
// producto 2026-10-11). Antes vivía copiada en AssetService, TechnicianService y ZoneService, cada copia con su
// propio parche para no duplicar filas contra el índice único TechnicianId+AssetId.
//
// Un TechnicianAsset concede permisos (qué máquinas ve el técnico y en cuáles registra tóner), así que el
// vínculo automático también se RETIRA cuando deja de aplicar: el técnico pierde la zona, el municipio sale de
// la zona, o el activo deja de estar Instalado en ella. Solo se retiran los de origen Zone: uno Manual es una
// decisión de un administrador (o la instalación que el técnico confirmó) y no depende de la cobertura.
//
// Sigue el patrón de colaborador del proyecto (ver ContractAssetService.AddAsync + AssetService.
// PrepareStatusChangeAsync): recibe el DbContext scoped del caller, deja los cambios trackeados y NO llama a
// SaveChangesAsync — el caller que orquesta la operación decide cuándo persistir, así que un cambio de zonas
// y los vínculos que provoca se confirman en el mismo guardado.
//
// Por eso mismo TODO lo que lee considera el estado pendiente del ChangeTracker, no solo la base: la zona de un
// municipio, las zonas de un técnico y los vínculos pueden haberse modificado en esta misma operación y todavía
// no estar guardados.
internal static class TechnicianAssetLinks
{
    // El activo es la entidad que cambió (estado o sede): vincula a los técnicos que cubren su zona.
    public static async Task SyncForAssetAsync(IApplicationDbContext db, Asset asset, CancellationToken cancellationToken)
    {
        var wantedTechnicianIds = await TechniciansCoveringAsync(db, asset, cancellationToken);
        var existing = await EffectiveLinksAsync(db, ta => ta.AssetId == asset.Id, cancellationToken);

        foreach (var technicianId in wantedTechnicianIds.Except(existing.Select(l => l.TechnicianId)))
        {
            db.TechnicianAssets.Add(new TechnicianAsset
            {
                TechnicianId = technicianId,
                AssetId = asset.Id,
                Source = TechnicianAssetSource.Zone
            });
        }

        // Un activo que salió de la zona (bodega, baja, otra sede) deja de estar cubierto por quien lo cubría.
        db.TechnicianAssets.RemoveRange(existing.Where(l =>
            l.Source == TechnicianAssetSource.Zone && !wantedTechnicianIds.Contains(l.TechnicianId)));
    }

    // Cambiaron las zonas de estos técnicos, o los municipios de una zona que ellos cubren: vincula los activos
    // Instalado de su cobertura efectiva.
    public static async Task SyncForTechniciansAsync(
        IApplicationDbContext db, IReadOnlyCollection<Guid> technicianIds, CancellationToken cancellationToken)
    {
        if (technicianIds.Count == 0)
        {
            return;
        }

        var ids = technicianIds.ToList();
        var technicianZones = await EffectiveTechnicianZonesAsync(db, tz => ids.Contains(tz.TechnicianId), cancellationToken);
        var existing = await EffectiveLinksAsync(db, ta => ids.Contains(ta.TechnicianId), cancellationToken);

        var wanted = await WantedPairsAsync(db, technicianZones, cancellationToken);
        var present = existing.Select(l => (l.TechnicianId, l.AssetId)).ToHashSet();

        foreach (var (technicianId, assetId) in wanted.Where(pair => !present.Contains(pair)))
        {
            db.TechnicianAssets.Add(new TechnicianAsset
            {
                TechnicianId = technicianId,
                AssetId = assetId,
                Source = TechnicianAssetSource.Zone
            });
        }

        // Lo que la cobertura efectiva ya no sostiene: un técnico sin zona queda sin vínculos automáticos.
        var stillWanted = wanted.ToHashSet();
        db.TechnicianAssets.RemoveRange(existing.Where(l =>
            l.Source == TechnicianAssetSource.Zone && !stillWanted.Contains((l.TechnicianId, l.AssetId))));
    }

    // Vínculo explícito (el técnico instaló el equipo, o lo hizo un administrador): idempotente, a prueba de que
    // un sync de la misma operación ya haya dejado el vínculo en el ChangeTracker sin guardar, y lo vuelve Manual
    // si existía por zona.
    public static async Task<TechnicianAsset> EnsureManualAsync(
        IApplicationDbContext db, Guid technicianId, Guid assetId, CancellationToken cancellationToken)
    {
        var existing = (await EffectiveLinksAsync(
                db, ta => ta.TechnicianId == technicianId && ta.AssetId == assetId, cancellationToken))
            .FirstOrDefault();

        if (existing is not null)
        {
            // Una decisión explícita gana sobre la derivada: si el vínculo existía por zona, pasa a manual y
            // deja de depender de la cobertura.
            existing.Source = TechnicianAssetSource.Manual;
            return existing;
        }

        var link = new TechnicianAsset { TechnicianId = technicianId, AssetId = assetId, Source = TechnicianAssetSource.Manual };
        db.TechnicianAssets.Add(link);
        return link;
    }

    private static async Task<IReadOnlyCollection<Guid>> TechniciansCoveringAsync(
        IApplicationDbContext db, Asset asset, CancellationToken cancellationToken)
    {
        if (asset.LifecycleStatus != AssetLifecycleStatus.Instalado || asset.CurrentClientLocationId is null)
        {
            return Array.Empty<Guid>();
        }

        var cityId = await db.ClientLocations
            .Where(cl => cl.Id == asset.CurrentClientLocationId)
            .Select(cl => (Guid?)cl.CityId)
            .FirstOrDefaultAsync(cancellationToken);
        if (cityId is null)
        {
            return Array.Empty<Guid>();
        }

        // Entidad trackeada: trae la ZoneId pendiente si esta misma operación movió el municipio de zona.
        var zoneId = (await db.Cities.FirstOrDefaultAsync(c => c.Id == cityId, cancellationToken))?.ZoneId;
        if (zoneId is null)
        {
            return Array.Empty<Guid>();
        }

        var zones = await EffectiveTechnicianZonesAsync(db, tz => tz.ZoneId == zoneId, cancellationToken);
        return zones.Select(tz => tz.TechnicianId).Distinct().ToList();
    }

    private static async Task<List<(Guid TechnicianId, Guid AssetId)>> WantedPairsAsync(
        IApplicationDbContext db, IReadOnlyCollection<TechnicianZone> technicianZones, CancellationToken cancellationToken)
    {
        var zoneIds = technicianZones.Select(tz => tz.ZoneId).Distinct().ToList();
        if (zoneIds.Count == 0)
        {
            return new();
        }

        // Municipios cuya zona EFECTIVA está entre las del técnico: los que la base ya ubica ahí (las instancias
        // trackeadas traen el valor pendiente, que puede haberlos sacado) más los que esta operación acaba de
        // mover hacia ahí.
        var fromDb = await db.Cities
            .Where(c => c.ZoneId != null && zoneIds.Contains(c.ZoneId.Value))
            .ToListAsync(cancellationToken);
        var cityZone = fromDb
            .Concat(db.Cities.Local)
            .Where(c => c.ZoneId != null && zoneIds.Contains(c.ZoneId.Value))
            .DistinctBy(c => c.Id)
            .ToDictionary(c => c.Id, c => c.ZoneId!.Value);
        if (cityZone.Count == 0)
        {
            return new();
        }

        var cityIds = cityZone.Keys.ToList();
        var installed = await db.Assets
            .Where(a => a.LifecycleStatus == AssetLifecycleStatus.Instalado
                && a.CurrentClientLocation != null
                && cityIds.Contains(a.CurrentClientLocation.CityId))
            .Select(a => new { a.Id, CityId = a.CurrentClientLocation!.CityId })
            .ToListAsync(cancellationToken);

        return technicianZones
            .SelectMany(tz => installed
                .Where(a => cityZone[a.CityId] == tz.ZoneId)
                .Select(a => (tz.TechnicianId, a.Id)))
            .Distinct()
            .ToList();
    }

    // Vínculos vigentes: lo que hay en la base MÁS lo agregado sin guardar, MENOS lo marcado para borrar.
    // Local excluye las entidades en estado Deleted, y tras LoadAsync contiene también las que acaba de traer.
    private static async Task<List<TechnicianAsset>> EffectiveLinksAsync(
        IApplicationDbContext db, Expression<Func<TechnicianAsset, bool>> predicate, CancellationToken cancellationToken)
    {
        await db.TechnicianAssets.Where(predicate).LoadAsync(cancellationToken);
        return db.TechnicianAssets.Local.Where(predicate.Compile()).ToList();
    }

    private static async Task<List<TechnicianZone>> EffectiveTechnicianZonesAsync(
        IApplicationDbContext db, Expression<Func<TechnicianZone, bool>> predicate, CancellationToken cancellationToken)
    {
        await db.TechnicianZones.Where(predicate).LoadAsync(cancellationToken);
        return db.TechnicianZones.Local.Where(predicate.Compile()).ToList();
    }
}
