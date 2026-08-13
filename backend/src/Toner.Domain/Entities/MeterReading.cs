using Toner.Domain.Common;

namespace Toner.Domain.Entities;

public class MeterReading : BaseEntity
{
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    public DateTime ReadingDate { get; set; } = DateTime.UtcNow;
    public long CounterValue { get; set; }

    public Guid? RegisteredByUserId { get; set; }
    public User? RegisteredByUser { get; set; }
}
