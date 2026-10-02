using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;

namespace Toner.Application.Common;

// Única definición de "qué municipios cubre un técnico": los de las zonas que tiene asignadas. Antes era una tabla
// aparte técnico-ciudad; ahora se deriva de TechnicianZone + City.ZoneId para que haya una sola fuente de verdad.
public static class CoverageQueries
{
    public static IQueryable<Guid> CoveredCityIds(this IApplicationDbContext db, Guid technicianId) =>
        db.Cities
            .Where(c => c.Zone != null && c.Zone.TechnicianZones.Any(tz => tz.TechnicianId == technicianId))
            .Select(c => c.Id);

    // Zona a la que pertenece un municipio (null si aún no tiene): es la que se descuenta del inventario.
    public static Task<Guid?> ZoneIdOfCityAsync(this IApplicationDbContext db, Guid cityId, CancellationToken cancellationToken = default) =>
        db.Cities.Where(c => c.Id == cityId).Select(c => c.ZoneId).FirstOrDefaultAsync(cancellationToken);
}
