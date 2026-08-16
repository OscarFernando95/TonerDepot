namespace Toner.Application.Assets.Dtos;

public class AssetDto
{
    public Guid Id { get; set; }
    public Guid AssetModelId { get; set; }
    public string AssetBrandName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = string.Empty;
    public string? Area { get; set; }
    public Guid? CurrentClientLocationId { get; set; }
    public string? CurrentClientLocationName { get; set; }
    public Guid? CurrentClientId { get; set; }
    public string? CurrentClientName { get; set; }
    public string? CityName { get; set; }
    public long? LastMeterReading { get; set; }
    public Guid? ActiveContractId { get; set; }
    public DateTime CreatedAt { get; set; }
}
