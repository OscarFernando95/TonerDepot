using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toner.Application.Common.Interfaces;
using Toner.Infrastructure.Email;
using Toner.Infrastructure.Notifications;
using Toner.Infrastructure.Security;

namespace Toner.Infrastructure.Jobs;

// Encolado por HangfireBackgroundJobScheduler al crear/resetear un usuario (hallazgo #3 de
// SECURITY_AUDIT.md). Recibe la contraseña ya cifrada (AesGcmJobPayloadEncryptor) — nunca en claro:
// eso es lo que termina en las tablas de Hangfire si el job queda en cola o reintentando.
//
// Attempts=10 es el default real de Hangfire (verificado contra el paquete instalado, no asumido); se
// deja explícito para documentar la intención. OnAttemptsExceeded=Delete: un fallo definitivo no debe
// quedar parado para siempre en la pestaña "Failed" del dashboard con el ciphertext todavía ahí.
[AutomaticRetry(Attempts = 10, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
public class SendGeneratedPasswordEmailJob
{
    private readonly IEmailSender _emailSender;
    private readonly IJobPayloadEncryptor _encryptor;
    private readonly IExceptionLogger _exceptionLogger;
    private readonly ILogger<SendGeneratedPasswordEmailJob> _logger;
    private readonly PasswordDeliverySettings _passwordDeliverySettings;

    public SendGeneratedPasswordEmailJob(
        IEmailSender emailSender,
        IJobPayloadEncryptor encryptor,
        IExceptionLogger exceptionLogger,
        ILogger<SendGeneratedPasswordEmailJob> logger,
        IOptions<PasswordDeliverySettings> passwordDeliverySettings)
    {
        _emailSender = emailSender;
        _encryptor = encryptor;
        _exceptionLogger = exceptionLogger;
        _logger = logger;
        _passwordDeliverySettings = passwordDeliverySettings.Value;
    }

    public async Task RunAsync(string cedula, string encryptedPassword, CancellationToken cancellationToken = default)
    {
        try
        {
            // Solo en memoria, solo el tiempo de armar el cuerpo del correo — nunca se loguea ni se
            // vuelve a persistir en claro.
            var plainPassword = _encryptor.Decrypt(encryptedPassword);

            var body =
                $"Cédula: {cedula}\n" +
                $"Contraseña temporal: {plainPassword}\n\n" +
                "Debe cambiarla en su próximo inicio de sesión.";

            await _emailSender.SendAsync(
                _passwordDeliverySettings.AdminEmail,
                "Nueva contraseña temporal generada",
                body,
                cancellationToken);

            // Nunca la contraseña acá (SECURITY_AUDIT.md hallazgo #8) — solo el hecho de que se envió.
            _logger.LogInformation(
                "Correo de contraseña temporal enviado para el usuario con cédula {Cedula}.", cedula);
        }
        catch (Exception ex)
        {
            await _exceptionLogger.LogAsync(
                source: $"Jobs.{nameof(SendGeneratedPasswordEmailJob)}",
                exception: ex,
                cancellationToken: CancellationToken.None);

            _logger.LogError(
                ex,
                "No se pudo enviar el correo de contraseña temporal para el usuario con cédula {Cedula}.",
                cedula);

            throw; // Hangfire debe seguir viendo la excepción para aplicar su política de reintentos.
        }
    }
}
