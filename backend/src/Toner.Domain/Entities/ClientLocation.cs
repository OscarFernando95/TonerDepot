using Toner.Domain.Common;

namespace Toner.Domain.Entities;

public class ClientLocation : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public Guid CityId { get; set; }
    public City City { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; } = true;

    // Coordenadas de la sede (WGS84), para verificar que el técnico llegó al sitio. Nullable: las sedes
    // existentes no las tienen; ambas van juntas o ninguna.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<ServiceTicket> ServiceTickets { get; set; } = new List<ServiceTicket>();
}
