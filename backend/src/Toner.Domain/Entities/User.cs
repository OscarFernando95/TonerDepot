using Toner.Domain.Common;

namespace Toner.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    // Solo aplica a usuarios con rol Cliente: a qué cliente pertenecen para el portal.
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    // Perfil de técnico asociado, si el usuario tiene rol Tecnico.
    public Technician? Technician { get; set; }
}
