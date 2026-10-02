namespace Toner.Application.Technicians.Dtos;

public class TechnicianZoneDto
{
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public IReadOnlyList<string> CityNames { get; set; } = Array.Empty<string>();
}

// Conjunto completo de zonas del técnico (normalmente una). Las que no estén en la lista se le quitan.
public class SetTechnicianZonesRequest
{
    public IReadOnlyList<Guid> ZoneIds { get; set; } = Array.Empty<Guid>();
}
