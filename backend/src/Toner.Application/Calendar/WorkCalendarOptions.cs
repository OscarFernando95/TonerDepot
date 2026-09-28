namespace Toner.Application.Calendar;

public class WorkCalendarOptions
{
    // Zona horaria de la empresa: los horarios laborales y los festivos se interpretan en ella.
    public string TimeZoneId { get; set; } = "America/Bogota";

    // Horario de un técnico que aún no tiene uno propio.
    public string DefaultWorkStart { get; set; } = "08:00";
    public string DefaultWorkEnd { get; set; } = "17:00";

    // 0 = domingo ... 6 = sábado. Sin valor inicial a propósito: el binder de configuración AGREGA los
    // elementos del JSON a un arreglo ya inicializado (quedarían duplicados). Si viene vacío, lun-vie.
    public int[] DefaultWorkDays { get; set; } = Array.Empty<int>();
}
