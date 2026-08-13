namespace Toner.Application.Assets.Dtos;

public class UpdateAssetRequest
{
    public Guid AssetBrandId { get; set; }
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
