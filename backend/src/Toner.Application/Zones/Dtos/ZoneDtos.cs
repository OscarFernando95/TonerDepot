namespace Toner.Application.Zones.Dtos;

public class ZoneCityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StateOrProvince { get; set; } = string.Empty;
}

public class ZoneDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<ZoneCityDto> Cities { get; set; } = Array.Empty<ZoneCityDto>();
    public int TechnicianCount { get; set; }
}

public class CreateZoneRequest
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateZoneRequest
{
    public string Name { get; set; } = string.Empty;
}

// Conjunto completo de municipios de la zona: los que no estén en la lista quedan sin zona y los que estén en otra
// zona se mueven a esta (un municipio pertenece a una sola zona).
public class SetZoneCitiesRequest
{
    public IReadOnlyList<Guid> CityIds { get; set; } = Array.Empty<Guid>();
}
