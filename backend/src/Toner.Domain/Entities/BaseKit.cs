using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Kit base de una MARCA: las piezas que se revisan en cada cambio de consumibles, agrupadas por unidad
// (p. ej. Ricoh: "Unidad fusora" → fusor, presor, uñas, termistores). Los modelos de la marca lo heredan.
public class BrandBaseItem : BaseEntity
{
    public Guid AssetBrandId { get; set; }
    public AssetBrand AssetBrand { get; set; } = null!;

    public Guid InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;

    public string GroupName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

// Ajuste de un MODELO sobre el kit de su marca. Si el ítem está en el kit de la marca, la fila lo excluye o le cambia
// grupo/cantidad; si no está, lo agrega solo a este modelo.
public class ModelBaseItem : BaseEntity
{
    public Guid AssetModelId { get; set; }
    public AssetModel AssetModel { get; set; } = null!;

    public Guid InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;

    public bool Excluded { get; set; }
    public string? GroupName { get; set; }
    public int? Quantity { get; set; }
}
