using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Activos cubiertos por un contrato. Un activo puede pasar por varios contratos a lo largo del tiempo.
public class ContractAsset : BaseEntity
{
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
