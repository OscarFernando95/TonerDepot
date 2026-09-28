namespace Toner.Application.Technicians.Dtos;

public class TechnicianDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    // Dentro de su horario laboral ahora mismo (día hábil, tramo vigente, sin permiso).
    public bool IsWorkingNow { get; set; }
    // Si está fuera de la oficina ahora, hasta cuándo (UTC); null si no.
    public DateTime? TimeOffUntil { get; set; }
    public IReadOnlyList<string> CoverageCityNames { get; set; } = Array.Empty<string>();
}
