using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;

namespace Toner.Application.Common;

// "Última lectura por activo" (y, para el promedio mensual de impresiones, también "primera
// lectura") es la agregación más repetida del sistema — estaba duplicada en 4 sitios
// (AssetService, MaintenanceScheduleService, ContractAssetService), cada uno trayendo TODAS las
// lecturas del conjunto de activos a memoria solo para quedarse con 1 o 2 por activo
// (CODE_QUALITY_AUDIT.md hallazgo #5). Agrupar por AssetId y unir de vuelta contra la fecha
// máxima/mínima deja la agregación en la base de datos: el resultado materializado es ~1 fila por
// activo (más algún empate raro), no 1 fila por lectura histórica.
internal static class MeterReadingQueries
{
    public static async Task<Dictionary<Guid, long>> GetLastReadingsByAssetAsync(
        IApplicationDbContext db, IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken)
    {
        if (assetIds.Count == 0)
        {
            return new Dictionary<Guid, long>();
        }

        var lastDateByAsset = db.MeterReadings
            .Where(m => assetIds.Contains(m.AssetId))
            .GroupBy(m => m.AssetId)
            .Select(g => new { AssetId = g.Key, LastReadingDate = g.Max(m => m.ReadingDate) });

        var rows = await db.MeterReadings
            .Where(m => assetIds.Contains(m.AssetId))
            .Join(
                lastDateByAsset,
                m => new { m.AssetId, m.ReadingDate },
                d => new { d.AssetId, ReadingDate = d.LastReadingDate },
                (m, d) => new { m.AssetId, m.CounterValue })
            .ToListAsync(cancellationToken);

        // Desempate determinista si dos lecturas del mismo activo comparten instante exacto (mismo
        // caso borde que ya documentaba el código que reemplaza este helper): el join puede devolver
        // más de una fila por activo en ese caso; se toma cualquiera de forma explícita en memoria,
        // sobre un resultado ya acotado a ~1 fila por activo, no sobre el historial completo.
        return rows
            .GroupBy(r => r.AssetId)
            .ToDictionary(g => g.Key, g => g.First().CounterValue);
    }

    public static async Task<(Dictionary<Guid, (DateTime ReadingDate, long CounterValue)> First, Dictionary<Guid, (DateTime ReadingDate, long CounterValue)> Last)>
        GetFirstAndLastReadingsByAssetAsync(
            IApplicationDbContext db, IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken)
    {
        if (assetIds.Count == 0)
        {
            return (new(), new());
        }

        var boundsByAsset = db.MeterReadings
            .Where(m => assetIds.Contains(m.AssetId))
            .GroupBy(m => m.AssetId)
            .Select(g => new { AssetId = g.Key, FirstDate = g.Min(m => m.ReadingDate), LastDate = g.Max(m => m.ReadingDate) });

        var firstRows = await db.MeterReadings
            .Where(m => assetIds.Contains(m.AssetId))
            .Join(
                boundsByAsset,
                m => new { m.AssetId, m.ReadingDate },
                b => new { b.AssetId, ReadingDate = b.FirstDate },
                (m, b) => new { m.AssetId, m.ReadingDate, m.CounterValue })
            .ToListAsync(cancellationToken);

        var lastRows = await db.MeterReadings
            .Where(m => assetIds.Contains(m.AssetId))
            .Join(
                boundsByAsset,
                m => new { m.AssetId, m.ReadingDate },
                b => new { b.AssetId, ReadingDate = b.LastDate },
                (m, b) => new { m.AssetId, m.ReadingDate, m.CounterValue })
            .ToListAsync(cancellationToken);

        var first = firstRows.GroupBy(r => r.AssetId).ToDictionary(g => g.Key, g => (g.First().ReadingDate, g.First().CounterValue));
        var last = lastRows.GroupBy(r => r.AssetId).ToDictionary(g => g.Key, g => (g.First().ReadingDate, g.First().CounterValue));

        return (first, last);
    }
}
