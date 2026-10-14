namespace Toner.Infrastructure.Jobs;

public static class RecurringJobTimeZone
{
    // Hangfire interpreta el cron en UTC si no se le da zona: Cron.Daily corría a las 00:00 UTC, que son
    // las 19:00 en Colombia, mientras que WorkCalendar interpreta horarios y festivos en America/Bogota
    // (AUDIT.md H-26). Los jobs recurrentes usan la misma zona del calendario laboral.
    //
    // Falla rápido con un mensaje claro si el id no existe en el sistema (p. ej. contenedor sin tzdata).
    public static TimeZoneInfo Resolve(string? timeZoneId)
    {
        var id = string.IsNullOrWhiteSpace(timeZoneId) ? "America/Bogota" : timeZoneId.Trim();
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException(
                $"La zona horaria '{id}' (WorkCalendar:TimeZoneId) no existe en este sistema. " +
                "Los jobs recurrentes la necesitan para programar su ejecución diaria.", ex);
        }
    }
}
