namespace Toner.Application.Users.Dtos;

public class CreateUserRequest
{
    // Credencial de login. Requerida, única.
    public string Cedula { get; set; } = string.Empty;

    // Contacto, opcional — ya no se usa para autenticar. La contraseña NO se pide acá: todo usuario
    // nuevo arranca con una generada por SecurePasswordGenerator y debe cambiarla en su próximo login.
    public string? Email { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public Guid CityId { get; set; }

    // Nombre canónico de Toner.Domain.Common.RoleNames (Administrador, Coordinador, Tecnico, Cliente, Ventas).
    public string RoleName { get; set; } = string.Empty;

    // Requerido únicamente cuando RoleName == Cliente.
    public Guid? ClientId { get; set; }
}
