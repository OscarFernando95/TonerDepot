namespace Toner.Domain.Common;

// Contraseña genérica asignada a todo usuario nuevo o reseteado por un Administrador. Fija a propósito
// (no generada al azar): el usuario debe cambiarla en su próximo login (ver User.MustChangePassword).
public static class PasswordDefaults
{
    public const string DefaultPassword = "Toner123";
}
