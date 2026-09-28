namespace Toner.Application.Calendar.Dtos;

public class WorkIntervalDto
{
    // 0 = domingo ... 6 = sábado.
    public int Day { get; set; }
    // "HH:mm", hora local de la empresa.
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
}

public class TechnicianScheduleDto
{
    // true: el técnico no tiene un horario propio y se muestra el de la empresa.
    public bool IsDefault { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public List<WorkIntervalDto> Intervals { get; set; } = new();
}

public class SetTechnicianScheduleRequest
{
    public List<WorkIntervalDto> Intervals { get; set; } = new();
}

public class TimeOffDto
{
    public Guid Id { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    // Fin real: si se canceló antes de tiempo, el momento de la cancelación.
    public DateTime EffectiveEndsAt { get; set; }
    public string? Reason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public bool IsActive { get; set; }
}

public class CreateTimeOffRequest
{
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string? Reason { get; set; }
}

public class HolidayDto
{
    // "yyyy-MM-dd"
    public string Date { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    // "legal" (calculado) | "custom" (ajuste de la empresa)
    public string Source { get; set; } = string.Empty;
    // true solo cuando un ajuste fuerza laborable un festivo legal.
    public bool IsWorkingDay { get; set; }
}

public class SetHolidayOverrideRequest
{
    // "yyyy-MM-dd"
    public string Date { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsWorkingDay { get; set; }
}
