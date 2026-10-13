using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Toner.Infrastructure.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;

    public SmtpEmailSender(IOptions<SmtpSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        // Texto plano únicamente: el contenido (cédula + contraseña) no necesita HTML, y así no hay
        // superficie de inyección de marcado que sanitizar.
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();

        // Modo configurado (Smtp:SecureSocketOptions, validado al arrancar): StartTls por defecto, que
        // FALLA si el servidor no ofrece STARTTLS en vez de seguir en texto plano. Antes se usaba
        // StartTlsWhenAvailable, vulnerable a STARTTLS stripping (AUDIT.md H-04). smtp4dev, que no ofrece
        // TLS, usa None — permitido solo en Development.
        await client.ConnectAsync(_settings.Host, _settings.Port, _settings.GetSecureSocketOptions(), cancellationToken);

        // El validador garantiza que usuario y contraseña vienen juntos (H-16).
        if (_settings.HasCredentials)
        {
            await client.AuthenticateAsync(_settings.Username!, _settings.Password!, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
