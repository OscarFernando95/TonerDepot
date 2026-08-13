namespace Toner.Application.Technicians.Dtos;

public class TechnicianCoverageDto
{
    public Guid Id { get; set; }
    public Guid CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
}
