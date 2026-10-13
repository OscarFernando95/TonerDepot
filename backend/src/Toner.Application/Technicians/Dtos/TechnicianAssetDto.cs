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

    // "Manual" (lo vinculó un administrador o el técnico lo instaló) | "Zone" (lo da la zona del técnico y se
    // retira solo si deja de cubrir el activo).
    public string Source { get; set; } = string.Empty;
}
