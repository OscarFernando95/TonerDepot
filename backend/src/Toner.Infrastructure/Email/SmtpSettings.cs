namespace Toner.Infrastructure.Email;

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }

    // Opcionales: smtp4dev (desarrollo) acepta relay anónimo, sin credenciales.
    public string? Username { get; set; }
    public string? Password { get; set; }

    public string FromAddress { get; set; } = string.Empty;
}
