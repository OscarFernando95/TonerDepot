using Toner.Domain.Common;

namespace Toner.Domain.Entities;

public class City : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string StateOrProvince { get; set; } = string.Empty;

    public ICollection<ClientLocation> ClientLocations { get; set; } = new List<ClientLocation>();

    // Zona de cobertura a la que pertenece el municipio (null = aún sin zona: ningún técnico lo cubre).
    public Guid? ZoneId { get; set; }
    public Zone? Zone { get; set; }
    public ICollection<User> Users { get; set; } = new List<User>();
}
