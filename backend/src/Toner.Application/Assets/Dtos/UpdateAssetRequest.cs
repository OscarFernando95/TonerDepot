namespace Toner.Application.Assets.Dtos;

public class UpdateAssetRequest
{
    public Guid AssetModelId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
