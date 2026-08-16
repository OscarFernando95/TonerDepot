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

    // Bloqueo de cuenta tras intentos fallidos de login consecutivos (ver SECURITY_AUDIT.md
    // hallazgo #9). Solo AuthService.LoginAsync los toca.
    public int FailedLoginAttempts { get; set; }

    // Null mientras la cuenta no está bloqueada. Se limpia en el próximo login exitoso; no hay
    // desbloqueo manual — expira sola cuando pasa la fecha.
    public DateTime? LockedOutUntil { get; set; }

    // Se embebe como claim en el JWT y se valida en cada request (ver Toner.Api.Auth.
    // SecurityStampValidator). Regenerarlo invalida todos los tokens ya emitidos de golpe — sin esto,
    // desactivar un usuario o resetear su contraseña no tenía ningún efecto sobre sesiones ya abiertas
    // (SECURITY_AUDIT.md hallazgo #6). Se regenera en ChangePasswordAsync, ResetPasswordAsync y
    // SetActiveStatusAsync.
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    // Solo aplica a usuarios con rol Cliente: a qué cliente pertenecen para el portal.
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    // Perfil de técnico asociado, si el usuario tiene rol Tecnico.
    public Technician? Technician { get; set; }
}
