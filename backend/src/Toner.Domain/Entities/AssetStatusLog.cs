using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

public class AssetStatusLog : BaseEntity
{
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    public AssetLifecycleStatus PreviousStatus { get; set; }
    public AssetLifecycleStatus NewStatus { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public Guid? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }

    public string? Notes { get; set; }
}
