using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

public class AssetStatusLog : BaseEntity
{
    public Guid AssetId { get; set; }

    // Denormalizado para la política RLS (fase 3b). CAPTURA AL ESCRIBIR desde Asset.ClientId TRAS la
    // mutación de estado, así que refleja a quién pertenece el activo COMO RESULTADO de la
    // transición. Nullable: una vuelta a bodega deja ClientId en NULL y ningún cliente ve ese log.
    // Es coherente y no pierde nada visible — en ese mismo momento el activo también desaparece de
    // la vista del cliente anterior. Más restrictivo, nunca más permisivo.
    public Guid? ClientId { get; set; }
    public Asset Asset { get; set; } = null!;

    public AssetLifecycleStatus PreviousStatus { get; set; }
    public AssetLifecycleStatus NewStatus { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public Guid? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }

    public string? Notes { get; set; }
}
