using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Sedes de su empresa que un usuario Cliente puede ver y en las que puede reportar tickets. Solo aplica si
// User.RestrictedToLocations es true; si es false, el usuario ve todas las sedes de su empresa y esta tabla no cuenta.
//
// ClientId se CAPTURA AL ESCRIBIR desde la sede (ClientLocation.ClientId es inmutable) para que la política RLS compare una
// columna propia y sargable, igual que ServiceTicket.ClientId. NOT NULL a propósito: no se puede crear sin decidir de
// qué cliente es. La restricción por sede en sí la hace cumplir la aplicación (ClientScope); RLS sigue aislando por empresa.
public class UserClientLocation : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid ClientLocationId { get; set; }
    public ClientLocation ClientLocation { get; set; } = null!;

    public Guid ClientId { get; set; }
}
