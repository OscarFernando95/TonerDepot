namespace Toner.Domain.Common;

public static class PasswordDefaults
{
    // Constante de conveniencia para tests que necesitan CUALQUIER contraseña fija conocida (login,
    // cambio de contraseña) — no tiene relación con la contraseña que recibe un usuario nuevo o
    // reseteado, que desde el hallazgo #3 de SECURITY_AUDIT.md es generada por SecurePasswordGenerator,
    // nunca este valor fijo.
    public const string DefaultPassword = "Toner123";

    // Único origen de la longitud mínima real: ChangePasswordRequestValidator (valida cualquier
    // contraseña nueva) y SecurePasswordGenerator (lo que UserService genera al crear/resetear un
    // usuario) leen de acá, para que no haya dos números que puedan desincronizarse.
    public const int MinimumLength = 10;
}
