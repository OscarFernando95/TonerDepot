using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Toner.Infrastructure.Email;

namespace Toner.Infrastructure.Health;

// Conecta y hace NOOP; NO autentica ni envía nada: un sondeo frecuente con AUTH podría disparar el
// bloqueo por intentos del proveedor. Detecta host caído o puerto cerrado, no credenciales erróneas.
public class SmtpHealthCheck : CachedHealthCheck
{
    private const int TimeoutMilliseconds = 5000;

    private readonly SmtpSettings _settings;

    public SmtpHealthCheck(IOptions<SmtpSettings> options, TimeSpan? ttl = null, TimeProvider? time = null)
        : base(ttl, time)
    {
        _settings = options.Value;
    }

    protected override async Task<HealthCheckResult> CheckCoreAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new SmtpClient { Timeout = TimeoutMilliseconds };
            // Mismas opciones de transporte que SmtpEmailSender, para comprobar el mismo camino.
            await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
            await client.NoOpAsync(cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "No se pudo contactar al servidor SMTP.", ex);
        }
    }
}
