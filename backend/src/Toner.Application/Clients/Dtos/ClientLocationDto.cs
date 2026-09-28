namespace Toner.Application.Clients.Dtos;

public class ClientLocationDto
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public Guid CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    // Coordenadas WGS84 de la sede (opcionales, ambas o ninguna): con ellas se verifica que el técnico llegó.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
