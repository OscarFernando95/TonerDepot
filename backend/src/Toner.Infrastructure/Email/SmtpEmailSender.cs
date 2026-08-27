using MailKit.Net.Smtp;
using MailKit.Security;
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

        // StartTlsWhenAvailable: se conecta en plano contra smtp4dev (que no ofrece STARTTLS) y se
        // actualiza sola contra un relay real que sí lo anuncie — un solo código sirve para ambos
        // entornos sin una bandera de configuración extra.
        await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);

        if (!string.IsNullOrEmpty(_settings.Username))
        {
            await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
