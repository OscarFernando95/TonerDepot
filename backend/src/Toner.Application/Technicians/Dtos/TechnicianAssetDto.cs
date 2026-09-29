namespace Toner.Application.Technicians.Dtos;

public class TechnicianAssetDto
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public string AssetBrandName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string? ClientLocationName { get; set; }
    public string? CityName { get; set; }
    public string? Area { get; set; }
}
