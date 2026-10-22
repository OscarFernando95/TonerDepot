namespace Toner.Application.Maintenance;

public class AbandonedSelfStartedOrderOptions
{
    // Horas que una orden iniciada por el propio técnico puede quedar Asignada sin check-in antes de caducar. Pasado
    // ese plazo se cancela sola: el técnico ya no va a atenderla y, abierta, bloquea al motor del cronograma para ese
    // equipo (MANT-2). Debe ser > 0.
    public int ExpireAfterHours { get; set; } = 4;
}
