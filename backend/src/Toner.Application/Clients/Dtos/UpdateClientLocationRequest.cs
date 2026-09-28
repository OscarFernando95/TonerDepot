namespace Toner.Application.Clients.Dtos;

public class UpdateClientLocationRequest
{
    public Guid CityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    // Coordenadas WGS84 de la sede (opcionales, ambas o ninguna): con ellas se verifica que el técnico llegó.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
