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

    // Vigencia del contrato activo del activo (null si no tiene contrato vinculado). Le permite al
    // cliente (app/web) restringir la fecha de lectura del check-out al rango real del contrato, en
    // vez de dejar elegir cualquier fecha — la validación real de todos modos vive en el backend
    // (TechnicianCheckInService.CheckOutAsync), esto es solo para no dejar que el técnico elija a
    // ciegas una fecha que el servidor va a rechazar.
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }

    // true si otro técnico ya tiene un check-in abierto sobre este activo — bloquea el check-in en la UI
    // en vez de dejar que el técnico lo intente y reciba el ConflictException recién en ese momento.
    public bool TakenByAnotherTechnician { get; set; }

    // false si AHORA el técnico no puede iniciar esta instalación por horario: fuera de su jornada, festivo o
    // permiso (un cliente 24/7 ignora la jornada pero no el permiso). El backend lo vuelve a exigir en el
    // check-in; esto solo evita que la UI ofrezca un botón que va a rechazarse.
    public bool CanStartNow { get; set; } = true;
}
