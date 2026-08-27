using System.Net.Mail;

namespace Toner.Infrastructure.Notifications;

// Mismo patrón que JwtSettingsValidator.
public static class PasswordDeliverySettingsValidator
{
    public static void EnsureValid(PasswordDeliverySettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.AdminEmail))
        {
            throw new InvalidOperationException("Falta la clave de configuración 'PasswordDelivery:AdminEmail'.");
        }

        try
        {
            _ = new MailAddress(settings.AdminEmail);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                $"'PasswordDelivery:AdminEmail' no es un correo válido: '{settings.AdminEmail}'.");
        }
    }
}
