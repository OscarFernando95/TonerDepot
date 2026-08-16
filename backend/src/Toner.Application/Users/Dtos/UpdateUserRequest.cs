namespace Toner.Application.Users.Dtos;

// Solo los datos de perfil/contacto son editables acá. Rol, cliente asociado y contraseña
// tienen sus propios flujos (reasignar rol tiene efectos en cascada — p.ej. crear/quitar el
// registro de Technician — que ameritan su propia revisión; la contraseña ya tiene el botón
// de "Restablecer contraseña").
public class UpdateUserRequest
{
    public string Cedula { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public Guid CityId { get; set; }
}
