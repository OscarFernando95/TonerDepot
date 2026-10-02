namespace Toner.Application.Inventory.Analytics;

// Cálculo puro (sin base de datos) del consumo de tóner por máquina. Una "entrega" es un registro de tóner con la
// cantidad y el contador de la máquina en ese momento.
public sealed record TonerEvent(Guid AssetId, Guid ItemId, DateTime At, int Units, long? Counter, bool? DeliveredToUser);

public sealed record TonerAssetInfo(
    Guid AssetId, Guid ModelId, string Brand, string Model, string SerialNumber, string? ClientName, string? LocationName, string? ZoneName);

// Un intervalo es lo que duró el tóner de una entrega: de su contador al de la siguiente entrega del MISMO tóner en la
// MISMA máquina. Se divide entre las unidades entregadas al inicio.
internal sealed record TonerInterval(Guid AssetId, Guid ItemId, DateTime ClosedAt, long Pages, int Units);

public static class TonerUsageCalculator
{
    public static (IReadOnlyList<TonerMachineRowDto> Machines, IReadOnlyList<TonerMonthDto> Monthly) Compute(
        IReadOnlyList<TonerEvent> events, IReadOnlyDictionary<Guid, TonerAssetInfo> assets, DateTime from, DateTime to)
    {
        var inRange = events.Where(e => e.At >= from && e.At <= to).ToList();

        // Intervalos por (máquina, tóner), solo entre entregas CON contador; un registro sin contador (p. ej. tóner
        // cargado como pieza de una visita sin lectura) cuenta como unidades pero no puede medir duración.
        var intervals = new List<TonerInterval>();
        foreach (var series in events.Where(e => e.Counter.HasValue).GroupBy(e => (e.AssetId, e.ItemId)))
        {
            var ordered = series.OrderBy(e => e.At).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var pages = ordered[i].Counter!.Value - ordered[i - 1].Counter!.Value;
                if (pages < 0 || ordered[i - 1].Units < 1)
                {
                    continue;
                }

                intervals.Add(new TonerInterval(series.Key.AssetId, series.Key.ItemId, ordered[i].At, pages, ordered[i - 1].Units));
            }
        }

        var rangeIntervals = intervals.Where(i => i.ClosedAt >= from && i.ClosedAt <= to).ToList();

        // Promedio de páginas por unidad de cada tóner en cada modelo (con al menos 2 máquinas que lo midieron).
        var modelAvg = rangeIntervals
            .Where(i => assets.ContainsKey(i.AssetId))
            .GroupBy(i => (assets[i.AssetId].ModelId, i.ItemId))
            .Where(g => g.Select(i => i.AssetId).Distinct().Count() >= 2 && g.Sum(i => i.Units) > 0)
            .ToDictionary(g => g.Key, g => (double)g.Sum(i => i.Pages) / g.Sum(i => i.Units));

        var rows = new List<TonerMachineRowDto>();
        foreach (var byAsset in inRange.GroupBy(e => e.AssetId))
        {
            if (!assets.TryGetValue(byAsset.Key, out var info))
            {
                continue;
            }

            var mine = rangeIntervals.Where(i => i.AssetId == byAsset.Key).ToList();
            var measuredUnits = mine.Sum(i => i.Units);
            var withCounter = byAsset.Where(e => e.Counter.HasValue).OrderBy(e => e.At).ToList();

            // Páginas del rango: del último contador anterior al rango (o del primero dentro) al último dentro.
            long? pagesInRange = null;
            if (withCounter.Count > 0)
            {
                var before = events.Where(e => e.AssetId == byAsset.Key && e.Counter.HasValue && e.At < from).OrderByDescending(e => e.At).FirstOrDefault();
                var start = before?.Counter ?? withCounter[0].Counter!.Value;
                pagesInRange = Math.Max(0, withCounter[^1].Counter!.Value - start);
            }

            // Lo esperado si cada tóner rindiera como el promedio de su modelo; lo real es lo que recorrió el contador.
            double? vsModel = null;
            var expected = 0.0;
            var comparable = true;
            foreach (var interval in mine)
            {
                if (modelAvg.TryGetValue((info.ModelId, interval.ItemId), out var avg))
                {
                    expected += avg * interval.Units;
                }
                else
                {
                    comparable = false;
                    break;
                }
            }

            if (comparable && mine.Count > 0 && expected > 0)
            {
                vsModel = Math.Round(mine.Sum(i => (double)i.Pages) / expected * 100, 1);
            }

            var last = byAsset.OrderByDescending(e => e.At).First();
            rows.Add(new TonerMachineRowDto
            {
                AssetId = info.AssetId, Brand = info.Brand, Model = info.Model, SerialNumber = info.SerialNumber,
                ClientName = info.ClientName, LocationName = info.LocationName, ZoneName = info.ZoneName,
                TotalUnits = byAsset.Sum(e => e.Units),
                ChangedByTechnicianUnits = byAsset.Where(e => e.DeliveredToUser == false).Sum(e => e.Units),
                DeliveredToUserUnits = byAsset.Where(e => e.DeliveredToUser == true).Sum(e => e.Units),
                MeasuredUnits = measuredUnits,
                AvgPagesPerUnit = measuredUnits > 0 ? Math.Round((double)mine.Sum(i => i.Pages) / measuredUnits, 1) : null,
                PagesInRange = pagesInRange,
                VsModelPercent = vsModel,
                LastEventAt = last.At,
                LastCounter = last.Counter
            });
        }

        var monthly = inRange
            .GroupBy(e => e.At.ToString("yyyy-MM"))
            .OrderBy(g => g.Key)
            .Select(g => new TonerMonthDto { Month = g.Key, Units = g.Sum(e => e.Units) })
            .ToList();

        return (rows.OrderByDescending(r => r.TotalUnits).ThenBy(r => r.Brand).ThenBy(r => r.Model).ToList(), monthly);
    }

    // Agrupa filas de máquina por una clave (cliente, zona, modelo) con el mismo promedio ponderado.
    public static IReadOnlyList<TonerGroupRowDto> GroupBy(IEnumerable<TonerMachineRowDto> rows, Func<TonerMachineRowDto, string?> key) =>
        rows.GroupBy(r => key(r) ?? "Sin asignar")
            .Select(g =>
            {
                var measured = g.Sum(r => r.MeasuredUnits);
                var pages = g.Sum(r => (r.AvgPagesPerUnit ?? 0) * r.MeasuredUnits);
                return new TonerGroupRowDto
                {
                    Name = g.Key,
                    Machines = g.Count(),
                    TotalUnits = g.Sum(r => r.TotalUnits),
                    AvgPagesPerUnit = measured > 0 ? Math.Round(pages / measured, 1) : null
                };
            })
            .OrderByDescending(g => g.TotalUnits)
            .ToList();
}
