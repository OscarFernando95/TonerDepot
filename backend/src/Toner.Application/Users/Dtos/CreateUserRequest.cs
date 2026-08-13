namespace Toner.Application.Users.Dtos;

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    // Nombre canónico de Toner.Domain.Common.RoleNames (Administrador, Coordinador, Tecnico, Cliente, Ventas).
    public string RoleName { get; set; } = string.Empty;

    // Requerido únicamente cuando RoleName == Cliente.
    public Guid? ClientId { get; set; }
}
