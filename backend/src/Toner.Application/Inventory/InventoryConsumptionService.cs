using Microsoft.EntityFrameworkCore;
using Toner.Application.Assets;
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
    private readonly IStockLock _stockLock;
    private readonly IMeterReadingRecorder _readings;

    public InventoryConsumptionService(IApplicationDbContext db, IBaseKitService kits, IStockLock stockLock, IMeterReadingRecorder readings)
    {
        _db = db;
        _kits = kits;
        _stockLock = stockLock;
        _readings = readings;
    }

    private sealed record Source(Guid LocationId, string Name, bool IsMain);

    // Debe llamarse dentro de IStockLock.RunInStockTransactionAsync (lo hace TechnicianCheckInService.CheckOutAsync):
    // bloquea el saldo de cada pieza hasta el SaveChanges del cierre.
    public async Task PrepareVisitConsumptionAsync(
        VisitConsumption visit, IReadOnlyList<UsedPartRequest> parts, CancellationToken cancellationToken = default)
    {
        if (parts.Count == 0)
        {
            return;
        }

        // El mismo ítem repetido se suma en una sola salida.
        var merged = parts.GroupBy(p => p.ItemId).ToDictionary(g => g.Key, g => g.Sum(p => p.Quantity));

        var ids = merged.Keys.ToList();
        var items = await _db.InventoryItems.Where(i => ids.Contains(i.Id)).Select(i => new { i.Id, i.Name, i.IsActive, i.Category }).ToListAsync(cancellationToken);
        var invalid = ids.Where(id => items.All(i => i.Id != id || !i.IsActive)).ToList();
        if (invalid.Count > 0)
        {
            throw new ConflictException("Alguna de las piezas no existe o está desactivada. Actualiza la lista e inténtalo de nuevo.");
        }

        var revealerIds = items.Where(i => i.Category == InventoryCategory.Revelador).Select(i => i.Id).ToHashSet();
        if (merged.Any(kv => !revealerIds.Contains(kv.Key) && (kv.Value < 1 || kv.Value > MaxQuantityPerPart)))
        {
            throw new ConflictException($"La cantidad de cada pieza debe estar entre 1 y {MaxQuantityPerPart}.");
        }

        await EnsureRevealerGramsMatchKitAsync(visit.AssetId, merged, items.Where(i => revealerIds.Contains(i.Id)).ToDictionary(i => i.Id, i => i.Name), cancellationToken);

        Guid? clientLocationId = null;
        if (!visit.AssetId.HasValue && visit.ServiceTicketId.HasValue)
        {
            clientLocationId = await _db.ServiceTickets
                .Where(t => t.Id == visit.ServiceTicketId.Value)
                .Select(t => (Guid?)t.ClientLocationId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var source = await ResolveSourceAsync(visit.AssetId, clientLocationId, cancellationToken);

        // El inventario nunca queda en negativo: no se consume lo que no existe (AUDIT.md H-41). Se bloquea el saldo
        // de TODAS las piezas antes de leerlo y, si alguna no alcanza, se rechaza el cierre entero nombrando cada
        // faltante — nada se descuenta a medias.
        await _stockLock.LockAsync(ids.Select(id => (id, source.LocationId)), cancellationToken);
        var balances = await BalancesAsync(source.LocationId, ids, cancellationToken);
        var shortages = items
            .Where(i => balances.GetValueOrDefault(i.Id) < merged[i.Id])
            .OrderBy(i => i.Name)
            .Select(i => revealerIds.Contains(i.Id)
                ? $"{i.Name} (hay {balances.GetValueOrDefault(i.Id)} g, necesitas {merged[i.Id]} g)"
                : $"{i.Name} (hay {balances.GetValueOrDefault(i.Id)}, necesitas {merged[i.Id]})")
            .ToList();
        if (shortages.Count > 0)
        {
            throw new ConflictException(
                $"Stock insuficiente en {source.Name}: {string.Join("; ", shortages)}. " +
                "Quita esas piezas o pide que repongan el inventario antes de cerrar la visita.");
        }

        var note = source.IsMain && (visit.AssetId is not null || clientLocationId is not null)
            ? "Sin zona para el municipio del equipo: descontado de la bodega principal."
            : null;

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
        }
    }

    // El revelador se descuenta en gramos y el gramaje lo fija el kit del modelo: el técnico no lo ajusta, así que lo
    // que llegue debe coincidir exactamente con el kit (y el ítem debe pertenecer a él).
    private async Task EnsureRevealerGramsMatchKitAsync(
        Guid? assetId, IReadOnlyDictionary<Guid, int> requested, IReadOnlyDictionary<Guid, string> revealerNames, CancellationToken cancellationToken)
    {
        if (revealerNames.Count == 0)
        {
            return;
        }

        if (!assetId.HasValue)
        {
            throw new ConflictException("El revelador solo se descuenta de equipos catalogados: su gramaje sale del kit del modelo.");
        }

        var modelId = await _db.Assets.Where(a => a.Id == assetId).Select(a => (Guid?)a.AssetModelId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Asset), assetId.Value);
        var kitGrams = (await _kits.GetModelKitAsync(modelId, cancellationToken))
            .Where(k => !k.Excluded && revealerNames.ContainsKey(k.ItemId))
            .ToDictionary(k => k.ItemId, k => k.Quantity);

        foreach (var (itemId, name) in revealerNames)
        {
            if (!kitGrams.TryGetValue(itemId, out var grams))
            {
                throw new ConflictException($"'{name}' no está en el kit de este modelo.");
            }

            if (requested[itemId] != grams)
            {
                throw new ConflictException($"El gramaje de '{name}' lo fija el kit del modelo ({grams} g) y no se puede cambiar.");
            }
        }
    }

    public async Task<VisitKitDto> GetKitForAssetAsync(Guid? assetId, Guid? clientLocationId = null, CancellationToken cancellationToken = default)
    {
        var source = await ResolveSourceAsync(assetId, clientLocationId, cancellationToken);
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
                Quantity = k.Quantity, Stock = balances.GetValueOrDefault(k.ItemId), GramsPerBag = k.GramsPerBag
            })
            .ToList();
        return dto;
    }

    public async Task<PagedResult<PartOptionDto>> SearchPartsAsync(
        Guid? assetId, string? search, int? page, int? pageSize, CancellationToken cancellationToken = default, string? category = null, Guid? clientLocationId = null)
    {
        var query = _db.InventoryItems.Where(i => i.IsActive && i.Category != InventoryCategory.Revelador);
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

        var source = await ResolveSourceAsync(assetId, clientLocationId, cancellationToken);
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

        if (request.CounterValue is null)
        {
            throw new ConflictException("El contador de la máquina es obligatorio al registrar un tóner.");
        }

        await EnsureCounterFitsAsync(asset.Id, request.CounterValue.Value, (request.OccurredAt ?? DateTime.UtcNow).ToUniversalTime(), cancellationToken);

        var source = await ResolveSourceAsync(asset.Id, null, cancellationToken);

        // El contador que acompaña al tóner también es una lectura de la máquina: sin esto quedaba solo en el
        // movimiento de inventario y "último contador" (activo, órdenes, contratos) no se enteraba. Cae en el mismo
        // SaveChanges del movimiento, dentro de la transacción de stock.
        await _readings.PrepareAsync(
            asset.Id, request.CounterValue.Value, (request.OccurredAt ?? DateTime.UtcNow).ToUniversalTime(), requester.UserId, cancellationToken);

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
            DeliveredToUser = request.DeliveredToUser,
            Notes = notes,
            CreatedByUserId = requester.UserId,
            OccurredAt = (request.OccurredAt ?? DateTime.UtcNow).ToUniversalTime()
        };

        // Sin tóner en la zona no se puede registrar uno instalado (AUDIT.md H-41): saldo bloqueado y comprobado
        // en la misma transacción que el descuento.
        await _stockLock.RunInStockTransactionAsync(
            async ct =>
            {
                await _stockLock.LockAsync(new[] { (item.Id, source.LocationId) }, ct);
                var available = (await BalancesAsync(source.LocationId, new[] { item.Id }, ct)).GetValueOrDefault(item.Id);
                if (available < request.Quantity)
                {
                    throw new ConflictException(
                        $"Stock insuficiente de \"{item.Name}\" en {source.Name}: hay {available} y se quieren registrar {request.Quantity}. " +
                        "Pide que repongan el inventario antes de registrarlo.");
                }

                _db.InventoryMovements.Add(movement);
                await _db.SaveChangesAsync(ct);
            },
            ct => _db.InventoryMovements.AnyAsync(m => m.Id == movement.Id, ct),
            cancellationToken);

        // StockWarning se conserva en el DTO por compatibilidad con los clientes; ya nunca hay negativo que avisar.
        return await TonerProjection(_db.InventoryMovements.Where(m => m.Id == movement.Id)).FirstAsync(cancellationToken);
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

    // El contador de la máquina solo sube: cada registro de tóner debe quedar entre el anterior y el siguiente en el
    // tiempo. Sin esto un contador mal tecleado arruina la duración estimada de todos los tóner de esa máquina.
    private async Task EnsureCounterFitsAsync(Guid assetId, long counter, DateTime occurredAt, CancellationToken cancellationToken)
    {
        var toner = _db.InventoryMovements.Where(m =>
            m.AssetId == assetId && m.Type == InventoryMovementType.Consumo && m.InventoryItem.Category == InventoryCategory.Toner && m.CounterValue != null);

        var previous = await toner.Where(m => m.OccurredAt <= occurredAt).OrderByDescending(m => m.OccurredAt).Select(m => m.CounterValue).FirstOrDefaultAsync(cancellationToken);
        if (previous.HasValue && counter < previous.Value)
        {
            throw new ConflictException($"El contador ({counter}) es menor al del registro de tóner anterior de esta máquina ({previous.Value}). Revisa el número.");
        }

        var next = await toner.Where(m => m.OccurredAt > occurredAt).OrderBy(m => m.OccurredAt).Select(m => m.CounterValue).FirstOrDefaultAsync(cancellationToken);
        if (next.HasValue && counter > next.Value)
        {
            throw new ConflictException($"El contador ({counter}) es mayor al del registro de tóner posterior de esta máquina ({next.Value}). Revisa el número o la fecha.");
        }
    }

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

    // De dónde se descuenta: la ubicación de la zona del municipio donde está instalado el equipo (o, si no hay
    // equipo catalogado, la zona del municipio de la SEDE del ticket — un ticket externo igual tiene una
    // ClientLocation conocida); si ninguna de las dos resuelve una zona, la bodega principal — nunca bloquea una
    // visita en campo. Antes solo miraba el equipo: un ticket de equipo no catalogado (sin AssetId) siempre caía a
    // la bodega principal aunque su sede sí tuviera zona asignada (hallazgo 2026-10-12).
    private async Task<Source> ResolveSourceAsync(Guid? assetId, Guid? clientLocationId, CancellationToken cancellationToken)
    {
        Guid? zoneId = null;
        if (assetId.HasValue)
        {
            zoneId = await _db.Assets
                .Where(a => a.Id == assetId.Value)
                .Select(a => a.CurrentClientLocation != null ? a.CurrentClientLocation.City.ZoneId : null)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else if (clientLocationId.HasValue)
        {
            zoneId = await _db.ClientLocations
                .Where(cl => cl.Id == clientLocationId.Value)
                .Select(cl => (Guid?)cl.City.ZoneId)
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
