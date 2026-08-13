using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Ciudades/zonas que un técnico puede cubrir. Un técnico puede tener varias.
public class TechnicianCoverage : BaseEntity
{
    public Guid TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;

    public Guid CityId { get; set; }
    public City City { get; set; } = null!;
}
