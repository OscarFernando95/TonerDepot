namespace Toner.Application.Assets.Dtos;

// Activo en AssetLifecycleStatus.PendienteInstalacion, listo para que cualquier técnico lo instale.
public class PendingInstallationDto
{
    public Guid AssetId { get; set; }
    public string AssetBrandName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;

    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public Guid ClientLocationId { get; set; }
    public string ClientLocationName { get; set; } = string.Empty;
    public string? CityName { get; set; }

    public Guid? ContractId { get; set; }

    // true si otro técnico ya tiene un check-in abierto sobre este activo — bloquea el check-in en la UI
    // en vez de dejar que el técnico lo intente y reciba el ConflictException recién en ese momento.
    public bool TakenByAnotherTechnician { get; set; }
}
