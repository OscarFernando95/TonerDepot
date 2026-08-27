using Toner.Infrastructure.Email;

namespace Toner.Application.Tests.TestSupport;

public sealed class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> SentEmails { get; } = new();

    // Para probar la política de reintentos de Hangfire sobre un fallo de envío sin un SMTP real.
    public Exception? ThrowOnSend { get; set; }

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSend is not null)
        {
            throw ThrowOnSend;
        }

        SentEmails.Add((to, subject, body));
        return Task.CompletedTask;
    }
}
