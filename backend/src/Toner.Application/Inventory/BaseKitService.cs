using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Inventory.Dtos;
using Toner.Domain.Entities;

namespace Toner.Application.Inventory;

public class BaseKitService : IBaseKitService
{
    private readonly IApplicationDbContext _db;

    public BaseKitService(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<BaseKitItemDto>> GetBrandKitAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        await EnsureBrandExistsAsync(brandId, cancellationToken);
        return await BrandKit(brandId).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BaseKitItemDto>> SetBrandKitAsync(Guid brandId, SetBrandKitRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureBrandExistsAsync(brandId, cancellationToken);
        await EnsureItemsExistAsync(request.Items.Select(i => i.ItemId), cancellationToken);

        var current = await _db.BrandBaseItems.Where(b => b.AssetBrandId == brandId).ToListAsync(cancellationToken);
        var wanted = request.Items.ToDictionary(i => i.ItemId);

        _db.BrandBaseItems.RemoveRange(current.Where(b => !wanted.ContainsKey(b.InventoryItemId)));

        foreach (var row in current.Where(b => wanted.ContainsKey(b.InventoryItemId)))
        {
            var req = wanted[row.InventoryItemId];
            row.GroupName = req.GroupName.Trim();
            row.Quantity = req.Quantity;
        }

        foreach (var req in request.Items.Where(i => current.All(b => b.InventoryItemId != i.ItemId)))
        {
            _db.BrandBaseItems.Add(new BrandBaseItem
            {
                AssetBrandId = brandId, InventoryItemId = req.ItemId, GroupName = req.GroupName.Trim(), Quantity = req.Quantity
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await BrandKit(brandId).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModelKitItemDto>> GetModelKitAsync(Guid modelId, CancellationToken cancellationToken = default)
    {
        var brandId = await BrandOfModelAsync(modelId, cancellationToken);
        return await EffectiveKitAsync(brandId, modelId, cancellationToken);
    }

    public async Task<IReadOnlyList<ModelKitItemDto>> SetModelKitAsync(Guid modelId, SetModelKitRequest request, CancellationToken cancellationToken = default)
    {
        var brandId = await BrandOfModelAsync(modelId, cancellationToken);
        await EnsureItemsExistAsync(request.Overrides.Select(o => o.ItemId), cancellationToken);

        var inBrand = (await _db.BrandBaseItems.Where(b => b.AssetBrandId == brandId).Select(b => b.InventoryItemId).ToListAsync(cancellationToken)).ToHashSet();

        // Un ítem que NO está en el kit de la marca solo se puede agregar (con su grupo), no "excluir".
        foreach (var o in request.Overrides.Where(o => !inBrand.Contains(o.ItemId)))
        {
            if (o.Excluded)
            {
                throw new ConflictException("No se puede excluir un ítem que no está en el kit de la marca.");
            }

            if (string.IsNullOrWhiteSpace(o.GroupName))
            {
                throw new ConflictException("Un ítem agregado solo al modelo necesita el grupo (unidad) al que pertenece.");
            }
        }

        var current = await _db.ModelBaseItems.Where(m => m.AssetModelId == modelId).ToListAsync(cancellationToken);
        var wanted = request.Overrides.ToDictionary(o => o.ItemId);

        _db.ModelBaseItems.RemoveRange(current.Where(m => !wanted.ContainsKey(m.InventoryItemId)));

        foreach (var row in current.Where(m => wanted.ContainsKey(m.InventoryItemId)))
        {
            Apply(row, wanted[row.InventoryItemId]);
        }

        foreach (var req in request.Overrides.Where(o => current.All(m => m.InventoryItemId != o.ItemId)))
        {
            var row = new ModelBaseItem { AssetModelId = modelId, InventoryItemId = req.ItemId };
            Apply(row, req);
            _db.ModelBaseItems.Add(row);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await EffectiveKitAsync(brandId, modelId, cancellationToken);
    }

    private static void Apply(ModelBaseItem row, ModelKitOverrideRequest req)
    {
        row.Excluded = req.Excluded;
        row.GroupName = string.IsNullOrWhiteSpace(req.GroupName) ? null : req.GroupName.Trim();
        row.Quantity = req.Quantity;
    }

    // Kit efectivo = kit de la marca, con lo que el modelo excluye o ajusta, más lo que el modelo agrega.
    private async Task<IReadOnlyList<ModelKitItemDto>> EffectiveKitAsync(Guid brandId, Guid modelId, CancellationToken cancellationToken)
    {
        var brandItems = await _db.BrandBaseItems.Where(b => b.AssetBrandId == brandId)
            .Select(b => new { b.InventoryItemId, ItemName = b.InventoryItem.Name, Category = b.InventoryItem.Category.ToString(), b.GroupName, b.Quantity })
            .ToListAsync(cancellationToken);
        var overrides = await _db.ModelBaseItems.Where(m => m.AssetModelId == modelId)
            .Select(m => new { m.InventoryItemId, ItemName = m.InventoryItem.Name, Category = m.InventoryItem.Category.ToString(), m.Excluded, m.GroupName, m.Quantity })
            .ToListAsync(cancellationToken);
        var overrideById = overrides.ToDictionary(o => o.InventoryItemId);

        var result = new List<ModelKitItemDto>();

        foreach (var b in brandItems)
        {
            overrideById.TryGetValue(b.InventoryItemId, out var o);
            result.Add(new ModelKitItemDto
            {
                ItemId = b.InventoryItemId,
                ItemName = b.ItemName,
                Category = b.Category,
                GroupName = o?.GroupName ?? b.GroupName,
                Quantity = o?.Quantity ?? b.Quantity,
                Excluded = o?.Excluded ?? false,
                Source = o is null ? "Marca" : "Modelo"
            });
        }

        var brandItemIds = brandItems.Select(b => b.InventoryItemId).ToHashSet();
        foreach (var o in overrides.Where(o => !brandItemIds.Contains(o.InventoryItemId)))
        {
            result.Add(new ModelKitItemDto
            {
                ItemId = o.InventoryItemId, ItemName = o.ItemName, Category = o.Category,
                GroupName = o.GroupName ?? string.Empty, Quantity = o.Quantity ?? 1, Excluded = false, Source = "Modelo"
            });
        }

        return result.OrderBy(r => r.GroupName).ThenBy(r => r.ItemName).ToList();
    }

    private IQueryable<BaseKitItemDto> BrandKit(Guid brandId) =>
        _db.BrandBaseItems
            .Where(b => b.AssetBrandId == brandId)
            .OrderBy(b => b.GroupName).ThenBy(b => b.InventoryItem.Name)
            .Select(b => new BaseKitItemDto
            {
                ItemId = b.InventoryItemId, ItemName = b.InventoryItem.Name, Category = b.InventoryItem.Category.ToString(),
                GroupName = b.GroupName, Quantity = b.Quantity
            });

    private async Task EnsureBrandExistsAsync(Guid brandId, CancellationToken cancellationToken)
    {
        if (!await _db.AssetBrands.AnyAsync(b => b.Id == brandId, cancellationToken))
        {
            throw new NotFoundException(nameof(AssetBrand), brandId);
        }
    }

    private async Task<Guid> BrandOfModelAsync(Guid modelId, CancellationToken cancellationToken) =>
        await _db.AssetModels.Where(m => m.Id == modelId).Select(m => (Guid?)m.AssetBrandId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(AssetModel), modelId);

    private async Task EnsureItemsExistAsync(IEnumerable<Guid> itemIds, CancellationToken cancellationToken)
    {
        var ids = itemIds.Distinct().ToList();
        if (ids.Count == 0) return;

        if (await _db.InventoryItems.CountAsync(i => ids.Contains(i.Id), cancellationToken) != ids.Count)
        {
            throw new NotFoundException(nameof(InventoryItem), "uno o más ítems del kit");
        }
    }
}
