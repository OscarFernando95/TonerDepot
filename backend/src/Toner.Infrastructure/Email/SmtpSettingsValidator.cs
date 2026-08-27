using System.Net.Mail;

namespace Toner.Infrastructure.Email;

// Mismo patrón que JwtSettingsValidator. Username/Password quedan fuera a propósito: son legítimamente
// opcionales (smtp4dev en desarrollo no pide auth), así que no se validan como requeridos.
public static class SmtpSettingsValidator
{
    public static void EnsureValid(SmtpSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            throw new InvalidOperationException("Falta la clave de configuración 'Smtp:Host'.");
        }

        if (settings.Port <= 0)
        {
            throw new InvalidOperationException(
                $"'Smtp:Port' debe ser mayor que 0. Valor actual: {settings.Port}.");
        }

        if (string.IsNullOrWhiteSpace(settings.FromAddress))
        {
            throw new InvalidOperationException("Falta la clave de configuración 'Smtp:FromAddress'.");
        }

        try
        {
            _ = new MailAddress(settings.FromAddress);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                $"'Smtp:FromAddress' no es un correo válido: '{settings.FromAddress}'.");
        }
    }
}
