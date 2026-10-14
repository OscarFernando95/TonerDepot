using System.Net.Mail;
using MailKit.Security;

namespace Toner.Infrastructure.Email;

// Mismo patrón que JwtSettingsValidator: falla al arrancar con un mensaje explícito en vez de arrancar en
// silencio con una configuración que luego falla (o filtra) en runtime.
public static class SmtpSettingsValidator
{
    // Únicos modos que GARANTIZAN TLS. StartTlsWhenAvailable y Auto se degradan a texto plano si el
    // servidor (o un atacante en la ruta que borre el anuncio STARTTLS) no lo ofrece — y por ese canal
    // viajan el AUTH del relay y la contraseña temporal del usuario (AUDIT.md H-04).
    private static readonly SecureSocketOptions[] ProductionModes =
    {
        SecureSocketOptions.StartTls,
        SecureSocketOptions.SslOnConnect
    };

    public static void EnsureValid(SmtpSettings settings, bool isDevelopment)
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

        // H-16: con usuario y sin contraseña la app arrancaba, cada envío lanzaba ArgumentNullException y
        // Hangfire terminaba descartando el job — la contraseña temporal no llegaba por ninguna vía.
        // Nunca se incluyen los valores en el mensaje.
        if (string.IsNullOrEmpty(settings.Username) != string.IsNullOrEmpty(settings.Password))
        {
            throw new InvalidOperationException(
                "'Smtp:Username' y 'Smtp:Password' deben configurarse juntos: ambos vacíos (relay anónimo) " +
                "o ambos con valor.");
        }

        var mode = ParseMode(settings.SecureSocketOptions);

        if (!isDevelopment && !ProductionModes.Contains(mode))
        {
            throw new InvalidOperationException(
                $"'Smtp:SecureSocketOptions' = '{mode}' no garantiza TLS y solo se admite en Development. " +
                $"Usa StartTls (puerto 587) o SslOnConnect (puerto 465).");
        }

        // Combinaciones que no fallan rápido sino que CUELGAN la conexión hasta el timeout (el cliente
        // espera un saludo en claro y el servidor un handshake TLS, o al revés).
        if (mode == SecureSocketOptions.StartTls && settings.Port == 465)
        {
            throw new InvalidOperationException(
                "'Smtp:Port' 465 es TLS implícito: con él usa 'Smtp:SecureSocketOptions' = SslOnConnect, " +
                "o cambia al puerto 587 para StartTls.");
        }

        if (mode == SecureSocketOptions.SslOnConnect && settings.Port is 587 or 25)
        {
            throw new InvalidOperationException(
                $"'Smtp:Port' {settings.Port} espera STARTTLS: con él usa 'Smtp:SecureSocketOptions' = StartTls, " +
                "o cambia al puerto 465 para SslOnConnect.");
        }
    }

    private static SecureSocketOptions ParseMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return SmtpSettings.DefaultSecureSocketOptions;
        }

        // TryParse + IsDefined (convención del proyecto, ver Common/EnumParsing.cs): TryParse solo acepta
        // también números ("99") que no son ningún valor definido.
        if (Enum.TryParse<SecureSocketOptions>(raw.Trim(), ignoreCase: true, out var mode) && Enum.IsDefined(mode))
        {
            return mode;
        }

        throw new InvalidOperationException(
            $"'Smtp:SecureSocketOptions' tiene un valor inválido: '{raw}'. Valores admitidos: " +
            $"{string.Join(", ", Enum.GetNames<SecureSocketOptions>())} (fuera de Development solo StartTls o SslOnConnect).");
    }
}
