namespace Toner.Application.Maintenance.Dtos;

public class CreateMaintenanceScheduleRequest
{
    public Guid AssetId { get; set; }
    public Guid? ContractId { get; set; }

    // Nombre del enum Toner.Domain.Enums.MaintenanceFrequencyType (PorContador, PorTiempo).
    public string FrequencyType { get; set; } = string.Empty;

    // Requerido cuando FrequencyType es PorContador.
    public int? PrintThreshold { get; set; }

    // Requerido cuando FrequencyType es PorTiempo.
    public int? TimeIntervalDays { get; set; }
}
