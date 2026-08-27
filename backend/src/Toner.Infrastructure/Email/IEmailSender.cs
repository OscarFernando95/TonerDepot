namespace Toner.Infrastructure.Email;

// Solo Infrastructure la consume (SendGeneratedPasswordEmailJob) — no hace falta una abstracción en
// Application porque ningún servicio de Application envía correo directamente, siempre a través de un
// job. Existe igual como interfaz (y no un SmtpEmailSender concreto inyectado a mano) para poder
// probar el job con un fake, sin un SMTP real.
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
