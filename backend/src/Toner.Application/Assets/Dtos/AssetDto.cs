namespace Toner.Application.Assets.Dtos;

public class AssetDto
{
    public Guid Id { get; set; }
    public Guid AssetBrandId { get; set; }
    public string AssetBrandName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = string.Empty;
    public Guid? CurrentClientLocationId { get; set; }
    public string? CurrentClientLocationName { get; set; }
    public string? CurrentClientName { get; set; }
    public DateTime CreatedAt { get; set; }
}
