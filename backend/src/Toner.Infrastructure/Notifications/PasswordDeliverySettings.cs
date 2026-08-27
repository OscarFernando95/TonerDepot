namespace Toner.Infrastructure.Notifications;

public class PasswordDeliverySettings
{
    // Dirección ADMINISTRATIVA fija que recibe cédula + contraseña generada de todo usuario nuevo o
    // reseteado — nunca el correo del propio funcionario (SECURITY_AUDIT.md hallazgo #3).
    public string AdminEmail { get; set; } = string.Empty;
}
