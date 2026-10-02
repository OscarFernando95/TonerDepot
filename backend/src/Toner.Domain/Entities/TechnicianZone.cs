using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Zonas que atiende un técnico (normalmente una). Su cobertura por municipio se deriva de las ciudades de sus zonas.
public class TechnicianZone : BaseEntity
{
    public Guid TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;

    public Guid ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;
}
