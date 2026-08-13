namespace Toner.Application.Cities.Dtos;

public class CityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StateOrProvince { get; set; } = string.Empty;
}
