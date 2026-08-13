using Toner.Domain.Common;

namespace Toner.Domain.Entities;

public class User : BaseEntity
{
    // Credencial de login (reemplaza al correo). Única, obligatoria.
    public string Cedula { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // "Celular", "Dirección" y "Ciudad": obligatorios al crear el usuario (ver CreateUserRequestValidator),
    // pero nullable a nivel de columna porque los usuarios ya existentes antes de este campo no tienen
    // un valor real que backfillear (a diferencia de Cedula, no hay un sintético razonable).
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public Guid? CityId { get; set; }
    public City? City { get; set; }

    // true por defecto: todo usuario nuevo (o reseteado) arranca con la contraseña genérica y debe
    // cambiarla antes de poder usar el resto de la app (ver MustChangePasswordMiddleware).
    public bool MustChangePassword { get; set; } = true;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    // Solo aplica a usuarios con rol Cliente: a qué cliente pertenecen para el portal.
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    // Perfil de técnico asociado, si el usuario tiene rol Tecnico.
    public Technician? Technician { get; set; }
}
