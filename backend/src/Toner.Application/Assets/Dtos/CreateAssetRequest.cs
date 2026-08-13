namespace Toner.Application.Assets.Dtos;

public class CreateAssetRequest
{
    public Guid AssetBrandId { get; set; }
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;

    // Nombre del enum Toner.Domain.Enums.AssetType (Impresora, ComputoEquipo).
    public string Type { get; set; } = string.Empty;
}
