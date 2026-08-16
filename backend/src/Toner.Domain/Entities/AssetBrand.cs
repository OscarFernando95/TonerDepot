using Toner.Domain.Common;

namespace Toner.Domain.Entities;

public class AssetBrand : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public ICollection<AssetModel> Models { get; set; } = new List<AssetModel>();
}
