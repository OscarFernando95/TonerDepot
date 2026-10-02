using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Application.Common.Paging;
using Toner.Domain.Enums;

namespace Toner.Application.Inventory.Analytics;

// BI de consumo de tóner por máquina, a partir de los registros de tóner (cada uno con su contador). Sin costos ni
// precios: por decisión, esta primera versión mide solo unidades y duración.
public class TonerAnalyticsService : ITonerAnalyticsService
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _time;

    public TonerAnalyticsService(IApplicationDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    private sealed record Computation(DateTime From, DateTime To, IReadOnlyList<TonerMachineRowDto> Machines, IReadOnlyList<TonerMonthDto> Monthly);

    public async Task<TonerSummaryDto> GetSummaryAsync(TonerFilter filter, CancellationToken cancellationToken = default)
    {
        var c = await ComputeAsync(filter, cancellationToken);
        var measured = c.Machines.Sum(m => m.MeasuredUnits);

        return new TonerSummaryDto
        {
            From = c.From,
            To = c.To,
            TotalUnits = c.Machines.Sum(m => m.TotalUnits),
            ChangedByTechnicianUnits = c.Machines.Sum(m => m.ChangedByTechnicianUnits),
            DeliveredToUserUnits = c.Machines.Sum(m => m.DeliveredToUserUnits),
            Machines = c.Machines.Count,
            MeasuredUnits = measured,
            AvgPagesPerUnit = measured > 0 ? Math.Round(c.Machines.Sum(m => (m.AvgPagesPerUnit ?? 0) * m.MeasuredUnits) / measured, 1) : null,
            Monthly = c.Monthly,
            ByClient = TonerUsageCalculator.GroupBy(c.Machines, m => m.ClientName),
            ByZone = TonerUsageCalculator.GroupBy(c.Machines, m => m.ZoneName),
            ByModel = TonerUsageCalculator.GroupBy(c.Machines, m => $"{m.Brand} {m.Model}")
        };
    }

    public async Task<PagedResult<TonerMachineRowDto>> ListMachinesAsync(TonerFilter filter, int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        var c = await ComputeAsync(filter, cancellationToken);
        var size = PagingDefaults.ResolvePageSize(pageSize);
        var number = PagingDefaults.ResolvePage(page);
        var items = c.Machines.Skip((number - 1) * size).Take(size).ToList();

        return new PagedResult<TonerMachineRowDto>
        {
            Items = items,
            PageSize = size,
            HasMore = number * size < c.Machines.Count,
            TotalCount = c.Machines.Count,
            Page = number
        };
    }

    public async Task<string> ExportCsvAsync(TonerFilter filter, CancellationToken cancellationToken = default)
    {
        var c = await ComputeAsync(filter, cancellationToken);
        var culture = CultureInfo.InvariantCulture;
        var sb = new StringBuilder("﻿");
        sb.AppendLine("Marca;Modelo;Serie;Cliente;Sede;Zona;Tóner usado;Cambiado por técnico;Entregado al usuario;Tóner con duración medida;Páginas por tóner;Páginas en el rango;Vs. promedio del modelo (%);Último registro;Último contador");

        foreach (var m in c.Machines)
        {
            sb.AppendLine(string.Join(';', new[]
            {
                Csv(m.Brand), Csv(m.Model), Csv(m.SerialNumber), Csv(m.ClientName), Csv(m.LocationName), Csv(m.ZoneName),
                m.TotalUnits.ToString(culture), m.ChangedByTechnicianUnits.ToString(culture), m.DeliveredToUserUnits.ToString(culture),
                m.MeasuredUnits.ToString(culture), m.AvgPagesPerUnit?.ToString("0.#", culture) ?? "", m.PagesInRange?.ToString(culture) ?? "",
                m.VsModelPercent?.ToString("0.#", culture) ?? "", m.LastEventAt?.ToString("yyyy-MM-dd HH:mm", culture) ?? "", m.LastCounter?.ToString(culture) ?? ""
            }));
        }

        return sb.ToString();
    }

    // Protege el CSV de comas/punto y coma/comillas, y de que una celda que empieza con = + - @ se ejecute como fórmula.
    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var v = value.Replace("\"", "\"\"");
        if (v.Length > 0 && "=+-@".Contains(v[0])) v = "'" + v;
        return $"\"{v}\"";
    }

    private async Task<Computation> ComputeAsync(TonerFilter filter, CancellationToken cancellationToken)
    {
        var to = (filter.To ?? _time.GetUtcNow().UtcDateTime).ToUniversalTime();
        var from = (filter.From ?? to.AddMonths(-6)).ToUniversalTime();
        if (from > to)
        {
            (from, to) = (to, from);
        }

        var assets = _db.Assets.AsQueryable();
        if (filter.ZoneId.HasValue)
            assets = assets.Where(a => a.CurrentClientLocation != null && a.CurrentClientLocation.City.ZoneId == filter.ZoneId.Value);
        if (filter.ClientId.HasValue)
            assets = assets.Where(a => a.ClientId == filter.ClientId.Value);
        if (filter.BrandId.HasValue)
            assets = assets.Where(a => a.AssetModel.AssetBrandId == filter.BrandId.Value);
        if (filter.ModelId.HasValue)
            assets = assets.Where(a => a.AssetModelId == filter.ModelId.Value);

        var rawEvents = await _db.InventoryMovements
            .Where(m => m.Type == InventoryMovementType.Consumo
                && m.InventoryItem.Category == InventoryCategory.Toner
                && m.AssetId != null
                && m.OccurredAt <= to
                && assets.Select(a => a.Id).Contains(m.AssetId.Value))
            .Select(m => new { AssetId = m.AssetId!.Value, m.InventoryItemId, m.OccurredAt, Units = -m.Delta, m.CounterValue, m.DeliveredToUser })
            .ToListAsync(cancellationToken);

        var assetIds = rawEvents.Select(e => e.AssetId).Distinct().ToList();
        var infos = await _db.Assets
            .Where(a => assetIds.Contains(a.Id))
            .Select(a => new
            {
                a.Id,
                a.AssetModelId,
                Brand = a.AssetModel.AssetBrand.Name,
                Model = a.AssetModel.Name,
                a.SerialNumber,
                ClientName = a.CurrentClientLocation != null ? a.CurrentClientLocation.Client.Name : null,
                LocationName = a.CurrentClientLocation != null ? a.CurrentClientLocation.Name : null,
                ZoneName = a.CurrentClientLocation != null && a.CurrentClientLocation.City.Zone != null ? a.CurrentClientLocation.City.Zone.Name : null
            })
            .ToListAsync(cancellationToken);

        var assetInfo = infos.ToDictionary(
            a => a.Id,
            a => new TonerAssetInfo(a.Id, a.AssetModelId, a.Brand, a.Model, a.SerialNumber, a.ClientName, a.LocationName, a.ZoneName));
        var events = rawEvents.Select(e => new TonerEvent(e.AssetId, e.InventoryItemId, e.OccurredAt, e.Units, e.CounterValue, e.DeliveredToUser)).ToList();

        var (machines, monthly) = TonerUsageCalculator.Compute(events, assetInfo, from, to);
        return new Computation(from, to, machines, monthly);
    }
}
