using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Agrupa municipios de cobertura. Es la unidad con la que se asigna el trabajo a los técnicos y, más adelante, la
// que lleva el inventario. Un municipio pertenece como máximo a una zona (City.ZoneId), así que a partir del
// municipio donde está instalado un equipo siempre se sabe a qué zona corresponde.
public class Zone : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public ICollection<City> Cities { get; set; } = new List<City>();
    public ICollection<TechnicianZone> TechnicianZones { get; set; } = new List<TechnicianZone>();
}
