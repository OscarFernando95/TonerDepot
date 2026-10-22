namespace Toner.Application.Maintenance.Dtos;

// El técnico asignado fija (o cambia) cuándo planea hacer el mantenimiento. UTC.
public class ScheduleMaintenanceOrderRequest
{
    public DateTime PlannedAt { get; set; }
}
