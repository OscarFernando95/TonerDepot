namespace Toner.Application.Assets.Dtos;

public class ChangeAssetStatusRequest
{
    // Nombre del enum Toner.Domain.Enums.AssetLifecycleStatus.
    public string NewStatus { get; set; } = string.Empty;

    // Obligatorio cuando NewStatus es Instalado (primera vez) o PendienteInstalacion.
    public Guid? ClientLocationId { get; set; }

    // Obligatorio cuando NewStatus es Instalado. Se limpia automáticamente al volver a EnBodega o al
    // pasar por PendienteInstalacion.
    public string? Area { get; set; }

    public string? Notes { get; set; }
}
