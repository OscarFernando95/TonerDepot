using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Un tramo laboral de un técnico en un día de la semana (hora local de la empresa). Un día puede tener
// varios tramos (ej. 08:00-12:00 y 14:00-18:00). Un técnico SIN tramos usa el horario por defecto de
// la empresa (ver WorkCalendarOptions).
public class TechnicianWorkInterval : BaseEntity
{
    public Guid TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;
    public DayOfWeek Day { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
