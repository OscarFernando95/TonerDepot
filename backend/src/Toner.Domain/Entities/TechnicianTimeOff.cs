using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// "Fuera de la oficina": permiso, vacaciones, incapacidad... Es temporal y con fecha de inicio y fin;
// NO es un retiro (eso es Technician.IsActive). Mientras dura, el técnico no es asignable y su tiempo
// no cuenta para el SLA de clientes en horario de oficina.
public class TechnicianTimeOff : BaseEntity
{
    public Guid TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string? Reason { get; set; }
    public Guid CreatedByUserId { get; set; }

    // Si se termina antes de tiempo (el técnico volvió antes) se marca acá; el período efectivo es
    // StartsAt..min(EndsAt, CancelledAt). Nunca se borra la fila: queda de registro.
    public DateTime? CancelledAt { get; set; }
}
