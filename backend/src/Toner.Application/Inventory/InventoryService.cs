using Microsoft.EntityFrameworkCore;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Common.Paging;
using Toner.Application.Inventory.Dtos;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Inventory;

public class InventoryService : IInventoryService
{
    private readonly IApplicationDbContext _db;

    public InventoryService(IApplicationDbContext db) => _db = db;

    // ── Catálogo ─────────────────────────────────────────────────────────────────────────────────

    public async Task<PagedResult<InventoryItemDto>> ListItemsAsync(
        string? search, string? category, bool activeOnly, int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        var query = _db.InventoryItems.AsQueryable();

        if (activeOnly)
        {
            query = query.Where(i => i.IsActive);
        }

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

        return await query
            .OrderBy(i => i.Name)
            .Select(i => new InventoryItemDto
            {
                Id = i.Id, Name = i.Name, Category = i.Category.ToString(), Unit = i.Unit,
                UnitCost = i.UnitCost, MinimumStock = i.MinimumStock, IsActive = i.IsActive
            })
            .ToOffsetPageAsync(page, pageSize, cancellationToken);
    }

    public async Task<InventoryItemDto> CreateItemAsync(CreateInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        await EnsureNameAvailableAsync(name, null, cancellationToken);

        var item = new InventoryItem
        {
            Name = name,
            Category = EnumParsing.ParseOrThrow<InventoryCategory>(request.Category, nameof(request.Category)),
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim(),
            UnitCost = request.UnitCost,
            MinimumStock = request.MinimumStock
        };
        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(item);
    }

    public async Task<InventoryItemDto> UpdateItemAsync(Guid id, UpdateInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryItem), id);

        var name = request.Name.Trim();
        await EnsureNameAvailableAsync(name, id, cancellationToken);

        item.Name = name;
        item.Category = EnumParsing.ParseOrThrow<InventoryCategory>(request.Category, nameof(request.Category));
        item.Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim();
        item.UnitCost = request.UnitCost;
        item.MinimumStock = request.MinimumStock;
        item.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(item);
    }

    // ── Ubicaciones ──────────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<InventoryLocationDto>> ListLocationsAsync(CancellationToken cancellationToken = default) =>
        await LocationProjection(_db.InventoryLocations)
            .OrderBy(l => l.Kind).ThenBy(l => l.Name)
            .ToListAsync(cancellationToken);

    public async Task<InventoryLocationDto> UpdateMainLocationAsync(UpdateMainLocationRequest request, CancellationToken cancellationToken = default)
    {
        var main = await _db.InventoryLocations.FirstOrDefaultAsync(l => l.Kind == InventoryLocationKind.Principal, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryLocation), "principal");

        if (request.CityId.HasValue && !await _db.Cities.AnyAsync(c => c.Id == request.CityId.Value, cancellationToken))
        {
            throw new NotFoundException(nameof(City), request.CityId.Value);
        }

        main.Name = request.Name.Trim();
        main.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        main.CityId = request.CityId;
        await _db.SaveChangesAsync(cancellationToken);

        return await LocationProjection(_db.InventoryLocations.Where(l => l.Id == main.Id)).FirstAsync(cancellationToken);
    }

    private static IQueryable<InventoryLocationDto> LocationProjection(IQueryable<InventoryLocation> query) =>
        query.Select(l => new InventoryLocationDto
        {
            Id = l.Id,
            Kind = l.Kind.ToString(),
            Name = l.Zone != null ? l.Zone.Name : (l.Name ?? "Bodega principal"),
            ZoneId = l.ZoneId,
            Address = l.Address,
            CityId = l.CityId,
            CityName = l.City != null ? l.City.Name : null
        });

    // ── Saldos y movimientos ─────────────────────────────────────────────────────────────────────

    public async Task<PagedResult<StockRowDto>> ListStockAsync(
        Guid? locationId, Guid? itemId, string? category, bool onlyLow, int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        var balances = _db.InventoryMovements
            .GroupBy(m => new { m.InventoryLocationId, m.InventoryItemId })
            .Select(g => new { g.Key.InventoryLocationId, g.Key.InventoryItemId, Quantity = g.Sum(m => m.Delta) });

        var rows = from b in balances
                   join i in _db.InventoryItems on b.InventoryItemId equals i.Id
                   join l in _db.InventoryLocations on b.InventoryLocationId equals l.Id
                   select new StockRowDto
                   {
                       ItemId = i.Id,
                       ItemName = i.Name,
                       Category = i.Category.ToString(),
                       LocationId = l.Id,
                       LocationName = l.Zone != null ? l.Zone.Name : (l.Name ?? "Bodega principal"),
                       Quantity = b.Quantity,
                       MinimumStock = i.MinimumStock,
                       IsLow = b.Quantity < 0 || (i.MinimumStock > 0 && b.Quantity <= i.MinimumStock)
                   };

        if (locationId.HasValue) rows = rows.Where(r => r.LocationId == locationId.Value);
        if (itemId.HasValue) rows = rows.Where(r => r.ItemId == itemId.Value);
        if (!string.IsNullOrWhiteSpace(category))
        {
            var parsed = EnumParsing.ParseOrThrow<InventoryCategory>(category, nameof(category)).ToString();
            rows = rows.Where(r => r.Category == parsed);
        }

        if (onlyLow) rows = rows.Where(r => r.IsLow);

        return await rows.OrderBy(r => r.ItemName).ThenBy(r => r.LocationName).ToOffsetPageAsync(page, pageSize, cancellationToken);
    }

    public async Task<PagedResult<InventoryMovementDto>> ListMovementsAsync(
        Guid? locationId, Guid? itemId, string? cursor, int? pageSize, CancellationToken cancellationToken = default)
    {
        var query = MovementProjection(_db.InventoryMovements.AsQueryable());

        if (locationId.HasValue) query = query.Where(m => m.LocationId == locationId.Value);
        if (itemId.HasValue) query = query.Where(m => m.ItemId == itemId.Value);

        var position = PageCursor.Decode(cursor);
        if (position is not null)
        {
            var (ts, lastId) = position.Value;
            query = query.Where(m => m.OccurredAt < ts || (m.OccurredAt == ts && m.Id.CompareTo(lastId) < 0));
        }

        return await query
            .OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.Id)
            .ToCursorPageAsync(pageSize, last => PageCursor.Encode(last.OccurredAt, last.Id), cancellationToken);
    }

    public async Task<InventoryMovementDto> RegisterEntryAsync(RegisterEntryRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsureItemAvailableAsync(request.ItemId, cancellationToken);
        await EnsureLocationExistsAsync(request.LocationId, cancellationToken);

        var movement = NewMovement(request.ItemId, request.LocationId, InventoryMovementType.Entrada, request.Quantity, request.Notes, userId, null);
        _db.InventoryMovements.Add(movement);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetMovementAsync(movement.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryMovementDto>> TransferAsync(TransferRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsureItemAvailableAsync(request.ItemId, cancellationToken);
        await EnsureLocationExistsAsync(request.FromLocationId, cancellationToken);
        await EnsureLocationExistsAsync(request.ToLocationId, cancellationToken);

        var available = await BalanceAsync(request.FromLocationId, request.ItemId, cancellationToken);
        if (available < request.Quantity)
        {
            throw new ConflictException($"Saldo insuficiente en el origen: hay {available} y se quieren traspasar {request.Quantity}.");
        }

        // Las dos filas del traspaso van en el mismo SaveChanges: o quedan ambas o ninguna.
        var transferId = Guid.NewGuid();
        var outgoing = NewMovement(request.ItemId, request.FromLocationId, InventoryMovementType.TraspasoSalida, -request.Quantity, request.Notes, userId, transferId);
        var incoming = NewMovement(request.ItemId, request.ToLocationId, InventoryMovementType.TraspasoEntrada, request.Quantity, request.Notes, userId, transferId);
        _db.InventoryMovements.AddRange(outgoing, incoming);
        await _db.SaveChangesAsync(cancellationToken);

        return await MovementProjection(_db.InventoryMovements.Where(m => m.TransferId == transferId))
            .OrderBy(m => m.Delta)
            .ToListAsync(cancellationToken);
    }

    public async Task<InventoryMovementDto> AdjustAsync(AdjustStockRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await _db.InventoryItems.AnyAsync(i => i.Id == request.ItemId, cancellationToken))
        {
            throw new NotFoundException(nameof(InventoryItem), request.ItemId);
        }

        await EnsureLocationExistsAsync(request.LocationId, cancellationToken);

        var movement = NewMovement(request.ItemId, request.LocationId, InventoryMovementType.Ajuste, request.Delta, request.Notes, userId, null);
        _db.InventoryMovements.Add(movement);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetMovementAsync(movement.Id, cancellationToken);
    }

    // ── Auxiliares ───────────────────────────────────────────────────────────────────────────────

    private static InventoryMovement NewMovement(
        Guid itemId, Guid locationId, InventoryMovementType type, int delta, string? notes, Guid userId, Guid? transferId) => new()
    {
        InventoryItemId = itemId,
        InventoryLocationId = locationId,
        Type = type,
        Delta = delta,
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        CreatedByUserId = userId,
        TransferId = transferId
    };

    private async Task<int> BalanceAsync(Guid locationId, Guid itemId, CancellationToken cancellationToken) =>
        await _db.InventoryMovements
            .Where(m => m.InventoryLocationId == locationId && m.InventoryItemId == itemId)
            .SumAsync(m => m.Delta, cancellationToken);

    private async Task EnsureItemAvailableAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var item = await _db.InventoryItems.Where(i => i.Id == itemId).Select(i => new { i.IsActive }).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryItem), itemId);

        if (!item.IsActive)
        {
            throw new ConflictException("El ítem está desactivado: reactívalo para registrar movimientos.");
        }
    }

    private async Task EnsureLocationExistsAsync(Guid locationId, CancellationToken cancellationToken)
    {
        if (!await _db.InventoryLocations.AnyAsync(l => l.Id == locationId, cancellationToken))
        {
            throw new NotFoundException(nameof(InventoryLocation), locationId);
        }
    }

    private async Task EnsureNameAvailableAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var lower = name.ToLower();
        if (await _db.InventoryItems.AnyAsync(i => i.Name.ToLower() == lower && i.Id != exceptId, cancellationToken))
        {
            throw new ConflictException($"Ya existe un ítem llamado '{name}'.");
        }
    }

    private async Task<InventoryMovementDto> GetMovementAsync(Guid id, CancellationToken cancellationToken) =>
        await MovementProjection(_db.InventoryMovements.Where(m => m.Id == id)).FirstAsync(cancellationToken);

    private IQueryable<InventoryMovementDto> MovementProjection(IQueryable<InventoryMovement> query) =>
        query.Select(m => new InventoryMovementDto
        {
            Id = m.Id,
            ItemId = m.InventoryItemId,
            ItemName = m.InventoryItem.Name,
            LocationId = m.InventoryLocationId,
            LocationName = m.InventoryLocation.Zone != null ? m.InventoryLocation.Zone.Name : (m.InventoryLocation.Name ?? "Bodega principal"),
            Type = m.Type.ToString(),
            Delta = m.Delta,
            Notes = m.Notes,
            CreatedByUserName = _db.Users.Where(u => u.Id == m.CreatedByUserId).Select(u => u.FullName).FirstOrDefault(),
            OccurredAt = m.OccurredAt
        });

    private static InventoryItemDto ToDto(InventoryItem i) => new()
    {
        Id = i.Id, Name = i.Name, Category = i.Category.ToString(), Unit = i.Unit,
        UnitCost = i.UnitCost, MinimumStock = i.MinimumStock, IsActive = i.IsActive
    };
}
