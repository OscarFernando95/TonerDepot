namespace Toner.Application.Cities.Dtos;

public class CreateCityRequest
{
    public string Name { get; set; } = string.Empty;
    public string? StateOrProvince { get; set; }
}
