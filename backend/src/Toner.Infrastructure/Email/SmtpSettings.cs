// Alias: la propiedad string SecureSocketOptions taparía al tipo homónimo de MailKit dentro de la clase.
using SocketMode = MailKit.Security.SecureSocketOptions;

namespace Toner.Infrastructure.Email;

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }

    // Opcionales, pero van JUNTOS: ambos vacíos (relay anónimo, p. ej. smtp4dev) o ambos con valor.
    // Lo exige SmtpSettingsValidator al arrancar (AUDIT.md H-16).
    public string? Username { get; set; }
    public string? Password { get; set; }

    public string FromAddress { get; set; } = string.Empty;

    // Nombre de MailKit.Security.SecureSocketOptions. String (no enum) para que un valor inválido lo
    // reporte SmtpSettingsValidator con un mensaje claro, en vez de un error opaco del binder.
    // Vacío = StartTls. Fuera de Development solo se aceptan StartTls y SslOnConnect (AUDIT.md H-04).
    public string? SecureSocketOptions { get; set; }

    public const SocketMode DefaultSecureSocketOptions = SocketMode.StartTls;

    public bool HasCredentials => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

    // Llamar solo después de SmtpSettingsValidator.EnsureValid (que ya rechazó valores inválidos).
    public SocketMode GetSecureSocketOptions() =>
        string.IsNullOrWhiteSpace(SecureSocketOptions)
            ? DefaultSecureSocketOptions
            : Enum.Parse<SocketMode>(SecureSocketOptions.Trim(), ignoreCase: true);
}
