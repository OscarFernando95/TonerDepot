namespace Toner.Application.Assets.Dtos;

public class ChangeAssetStatusRequest
{
    // Nombre del enum Toner.Domain.Enums.AssetLifecycleStatus.
    public string NewStatus { get; set; } = string.Empty;

    // Obligatorio cuando NewStatus es Instalado.
    public Guid? ClientLocationId { get; set; }

    public string? Notes { get; set; }
}
