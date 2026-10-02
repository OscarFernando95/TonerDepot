using Microsoft.EntityFrameworkCore;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Common.Paging;
using Toner.Application.Inventory.Dtos;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Inventory;

public class InventoryConsumptionService : IInventoryConsumptionService
{
    private const int MaxQuantityPerPart = 100;

    private readonly IApplicationDbContext _db;
    private readonly IBaseKitService _kits;

    public InventoryConsumptionService(IApplicationDbContext db, IBaseKitService kits)
    {
        _db = db;
        _kits = kits;
    }

    private sealed record Source(Guid LocationId, string Name, bool IsMain);

    public async Task<IReadOnlyList<string>> PrepareVisitConsumptionAsync(
        VisitConsumption visit, IReadOnlyList<UsedPartRequest> parts, CancellationToken cancellationToken = default)
    {
        if (parts.Count == 0)
        {
            return Array.Empty<string>();
        }

        // El mismo ítem repetido se suma en una sola salida.
        var merged = parts.GroupBy(p => p.ItemId).ToDictionary(g => g.Key, g => g.Sum(p => p.Quantity));
        if (merged.Values.Any(q => q < 1 || q > MaxQuantityPerPart))
        {
            throw new ConflictException($"La cantidad de cada pieza debe estar entre 1 y {MaxQuantityPerPart}.");
        }

        var ids = merged.Keys.ToList();
        var items = await _db.InventoryItems.Where(i => ids.Contains(i.Id)).Select(i => new { i.Id, i.Name, i.IsActive }).ToListAsync(cancellationToken);
        var invalid = ids.Where(id => items.All(i => i.Id != id || !i.IsActive)).ToList();
        if (invalid.Count > 0)
        {
            throw new ConflictException("Alguna de las piezas no existe o está desactivada. Actualiza la lista e inténtalo de nuevo.");
        }

        var source = await ResolveSourceAsync(visit.AssetId, cancellationToken);
        var balances = await BalancesAsync(source.LocationId, ids, cancellationToken);
        var warnings = new List<string>();
        var note = source.IsMain && visit.AssetId is not null ? "Sin zona para el municipio del equipo: descontado de la bodega principal." : null;

        foreach (var item in items)
        {
            var quantity = merged[item.Id];
            _db.InventoryMovements.Add(new InventoryMovement
            {
                InventoryItemId = item.Id,
                InventoryLocationId = source.LocationId,
                Type = InventoryMovementType.Consumo,
                Delta = -quantity,
                ClientId = visit.ClientId,
                AssetId = visit.AssetId,
                MaintenanceOrderId = visit.MaintenanceOrderId,
                ServiceTicketId = visit.ServiceTicketId,
                TimeLogId = visit.TimeLogId,
                CounterValue = visit.CounterValue,
                Notes = note,
                CreatedByUserId = visit.UserId
            });

            var after = balances.GetValueOrDefault(item.Id) - quantity;
            if (after < 0)
            {
                warnings.Add($"Stock insuficiente de \"{item.Name}\" en {source.Name}: quedó en {after}. Pide reponerlo.");
            }
        }

        return warnings;
    }

    public async Task<VisitKitDto> GetKitForAssetAsync(Guid? assetId, CancellationToken cancellationToken = default)
    {
        var source = await ResolveSourceAsync(assetId, cancellationToken);
        var dto = new VisitKitDto { AssetId = assetId, LocationId = source.LocationId, LocationName = source.Name, UsesMainWarehouse = source.IsMain };

        if (assetId is null)
        {
            return dto;
        }

        var modelId = await _db.Assets.Where(a => a.Id == assetId).Select(a => (Guid?)a.AssetModelId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Asset), assetId.Value);

        var kit = (await _kits.GetModelKitAsync(modelId, cancellationToken)).Where(k => !k.Excluded).ToList();
        var balances = await BalancesAsync(source.LocationId, kit.Select(k => k.ItemId).ToList(), cancellationToken);

        dto.Items = kit
            .Select(k => new VisitKitItemDto
            {
                ItemId = k.ItemId, ItemName = k.ItemName, Category = k.Category, GroupName = k.GroupName,
                Quantity = k.Quantity, Stock = balances.GetValueOrDefault(k.ItemId)
            })
            .ToList();
        return dto;
    }

    public async Task<PagedResult<PartOptionDto>> SearchPartsAsync(
        Guid? assetId, string? search, int? page, int? pageSize, CancellationToken cancellationToken = default, string? category = null)
    {
        var query = _db.InventoryItems.Where(i => i.IsActive);
        if (!string.IsNullOrWhiteSpace(category))
        {
            var parsed = EnumParsing.ParseOrThrow<InventoryCategory>(category, nameof(category));
            query = query.Where(i => i.Category == parsed);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(term));
        }

        var result = await query
            .OrderBy(i => i.Name)
            .Select(i => new PartOptionDto { ItemId = i.Id, Name = i.Name, Category = i.Category.ToString(), Unit = i.Unit })
            .ToOffsetPageAsync(page, pageSize, cancellationToken);

        var source = await ResolveSourceAsync(assetId, cancellationToken);
        var balances = await BalancesAsync(source.LocationId, result.Items.Select(i => i.ItemId).ToList(), cancellationToken);
        foreach (var option in result.Items)
        {
            option.Stock = balances.GetValueOrDefault(option.ItemId);
        }

        return result;
    }

    public async Task<TonerEntryDto> RegisterTonerAsync(RegisterTonerRequest request, RequestingUser requester, CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets.Where(a => a.Id == request.AssetId).Select(a => new { a.Id, a.ClientId }).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Asset), request.AssetId);

        await EnsureCanAccessAssetAsync(requester, asset.Id, cancellationToken);

        var item = await _db.InventoryItems.Where(i => i.Id == request.ItemId).Select(i => new { i.Id, i.Name, i.Category, i.IsActive }).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryItem), request.ItemId);
        if (item.Category != InventoryCategory.Toner)
        {
            throw new ConflictException("El ítem seleccionado no es un tóner.");
        }

        if (!item.IsActive)
        {
            throw new ConflictException("El tóner está desactivado.");
        }

        var source = await ResolveSourceAsync(asset.Id, cancellationToken);
        var balance = (await BalancesAsync(source.LocationId, new[] { item.Id }, cancellationToken)).GetValueOrDefault(item.Id) - request.Quantity;

        var how = request.DeliveredToUser ? "Entregado al usuario para que lo cambie" : "Cambiado por el técnico en la máquina";
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? how : $"{how}. {request.Notes.Trim()}";
        var movement = new InventoryMovement
        {
            InventoryItemId = item.Id,
            InventoryLocationId = source.LocationId,
            Type = InventoryMovementType.Consumo,
            Delta = -request.Quantity,
            ClientId = asset.ClientId,
            AssetId = asset.Id,
            CounterValue = request.CounterValue,
            Notes = notes,
            CreatedByUserId = requester.UserId,
            OccurredAt = (request.OccurredAt ?? DateTime.UtcNow).ToUniversalTime()
        };
        _db.InventoryMovements.Add(movement);
        await _db.SaveChangesAsync(cancellationToken);

        var entry = await TonerProjection(_db.InventoryMovements.Where(m => m.Id == movement.Id)).FirstAsync(cancellationToken);
        entry.StockWarning = balance < 0 ? $"Stock insuficiente de \"{item.Name}\" en {source.Name}: quedó en {balance}." : null;
        return entry;
    }

    public async Task<PagedResult<TonerEntryDto>> ListTonerAsync(
        Guid assetId, DateTime? from, DateTime? to, string? cursor, int? pageSize, RequestingUser requester, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessAssetAsync(requester, assetId, cancellationToken);

        var query = _db.InventoryMovements.Where(m =>
            m.AssetId == assetId && m.Type == InventoryMovementType.Consumo && m.InventoryItem.Category == InventoryCategory.Toner);
        if (from.HasValue) query = query.Where(m => m.OccurredAt >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(m => m.OccurredAt <= to.Value.ToUniversalTime());

        var position = PageCursor.Decode(cursor);
        if (position is not null)
        {
            var (ts, lastId) = position.Value;
            query = query.Where(m => m.OccurredAt < ts || (m.OccurredAt == ts && m.Id.CompareTo(lastId) < 0));
        }

        return await TonerProjection(query.OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.Id))
            .ToCursorPageAsync(pageSize, last => PageCursor.Encode(last.OccurredAt, last.MovementId), cancellationToken);
    }

    // ── Auxiliares ───────────────────────────────────────────────────────────────────────────────

    // El staff ve cualquier máquina; un técnico solo las que tiene vinculadas (la lista de sus máquinas).
    private async Task EnsureCanAccessAssetAsync(RequestingUser requester, Guid assetId, CancellationToken cancellationToken)
    {
        if (requester.IsStaff)
        {
            return;
        }

        var technicianId = requester.TechnicianId;
        var linked = requester.IsTechnician && technicianId.HasValue
            && await _db.TechnicianAssets.AnyAsync(ta => ta.TechnicianId == technicianId.Value && ta.AssetId == assetId, cancellationToken);
        if (!linked)
        {
            throw new ForbiddenException("Esta máquina no está vinculada a ti.");
        }
    }

    // De dónde se descuenta: la ubicación de la zona del municipio donde está instalado el equipo; si no tiene zona (o
    // el equipo no está catalogado), la bodega principal — nunca bloquea una visita en campo.
    private async Task<Source> ResolveSourceAsync(Guid? assetId, CancellationToken cancellationToken)
    {
        Guid? zoneId = null;
        if (assetId.HasValue)
        {
            zoneId = await _db.Assets
                .Where(a => a.Id == assetId.Value)
                .Select(a => a.CurrentClientLocation != null ? a.CurrentClientLocation.City.ZoneId : null)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (zoneId.HasValue)
        {
            var zoneLocation = await _db.InventoryLocations
                .Where(l => l.ZoneId == zoneId.Value)
                .Select(l => new { l.Id, Name = l.Zone!.Name })
                .FirstOrDefaultAsync(cancellationToken);
            if (zoneLocation is not null)
            {
                return new Source(zoneLocation.Id, zoneLocation.Name, false);
            }
        }

        var main = await _db.InventoryLocations
            .Where(l => l.Kind == InventoryLocationKind.Principal)
            .Select(l => new { l.Id, Name = l.Name ?? "Bodega principal" })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryLocation), "principal");
        return new Source(main.Id, main.Name, true);
    }

    private async Task<Dictionary<Guid, int>> BalancesAsync(Guid locationId, IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken)
    {
        if (itemIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await _db.InventoryMovements
            .Where(m => m.InventoryLocationId == locationId && itemIds.Contains(m.InventoryItemId))
            .GroupBy(m => m.InventoryItemId)
            .Select(g => new { ItemId = g.Key, Quantity = g.Sum(m => m.Delta) })
            .ToDictionaryAsync(x => x.ItemId, x => x.Quantity, cancellationToken);
    }

    private IQueryable<TonerEntryDto> TonerProjection(IQueryable<InventoryMovement> query) =>
        query.Select(m => new TonerEntryDto
        {
            MovementId = m.Id,
            ItemId = m.InventoryItemId,
            ItemName = m.InventoryItem.Name,
            Quantity = -m.Delta,
            OccurredAt = m.OccurredAt,
            CounterValue = m.CounterValue,
            Notes = m.Notes,
            RegisteredBy = _db.Users.Where(u => u.Id == m.CreatedByUserId).Select(u => u.FullName).FirstOrDefault()
        });
}
