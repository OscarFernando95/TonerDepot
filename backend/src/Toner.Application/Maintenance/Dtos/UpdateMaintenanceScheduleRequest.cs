namespace Toner.Application.Maintenance.Dtos;

public class UpdateMaintenanceScheduleRequest
{
    // Solo se actualiza el umbral relevante al FrequencyType con el que se creó el cronograma.
    public int? PrintThreshold { get; set; }
    public int? TimeIntervalDays { get; set; }
}
