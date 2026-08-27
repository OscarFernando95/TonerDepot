using Microsoft.Extensions.Options;
using Toner.Application.Tests.TestSupport;
using Toner.Infrastructure.Jobs;
using Toner.Infrastructure.Notifications;
using Toner.Infrastructure.Security;

namespace Toner.Application.Tests.Jobs;

// Hallazgo #3 de SECURITY_AUDIT.md: la contraseña generada debe llegar por correo, pero NUNCA debe
// quedar en ningún log — ni en el éxito ni en el fallo (mismo criterio del hallazgo #8).
public class SendGeneratedPasswordEmailJobTests
{
    private const string AdminEmail = "notificaciones-rrhh@toner.local";
    private const string Cedula = "1112223334";
    private const string PlainPassword = "Passw0rdSegura!";

    private static SendGeneratedPasswordEmailJob BuildJob(
        FakeEmailSender emailSender,
        FakeExceptionLogger exceptionLogger,
        CapturingLogger<SendGeneratedPasswordEmailJob> logger) =>
        new(
            emailSender,
            new AesGcmJobPayloadEncryptor(Options.Create(new JobPayloadEncryptionSettings { Key = Convert.ToBase64String(new byte[32]) })),
            exceptionLogger,
            logger,
            Options.Create(new PasswordDeliverySettings { AdminEmail = AdminEmail }));

    private static string Encrypt(string plaintext) =>
        new AesGcmJobPayloadEncryptor(Options.Create(new JobPayloadEncryptionSettings { Key = Convert.ToBase64String(new byte[32]) }))
            .Encrypt(plaintext);

    [Fact]
    public async Task RunAsync_Exito_EnviaCorreoALaDireccionAdministrativaConLaContraseñaDescifrada()
    {
        var emailSender = new FakeEmailSender();
        var job = BuildJob(emailSender, new FakeExceptionLogger(), new CapturingLogger<SendGeneratedPasswordEmailJob>());

        await job.RunAsync(Cedula, Encrypt(PlainPassword));

        var sent = Assert.Single(emailSender.SentEmails);
        Assert.Equal(AdminEmail, sent.To);
        Assert.Contains(Cedula, sent.Body);
        Assert.Contains(PlainPassword, sent.Body);
    }

    [Fact]
    public async Task RunAsync_Exito_NuncaLoguéaLaContraseñaEnClaroNiCifrada()
    {
        var emailSender = new FakeEmailSender();
        var logger = new CapturingLogger<SendGeneratedPasswordEmailJob>();
        var encrypted = Encrypt(PlainPassword);
        var job = BuildJob(emailSender, new FakeExceptionLogger(), logger);

        await job.RunAsync(Cedula, encrypted);

        AssertNoLogMentionsPassword(logger, encrypted);
    }

    [Fact]
    public async Task RunAsync_FalloDeEnvio_RegistraElFalloYRelanza_SinLoguearLaContraseña()
    {
        var emailSender = new FakeEmailSender { ThrowOnSend = new InvalidOperationException("SMTP caído") };
        var exceptionLogger = new FakeExceptionLogger();
        var logger = new CapturingLogger<SendGeneratedPasswordEmailJob>();
        var encrypted = Encrypt(PlainPassword);
        var job = BuildJob(emailSender, exceptionLogger, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.RunAsync(Cedula, encrypted));

        // Debe quedar registrado que falló (para que alguien lo note), pero sin la contraseña.
        var loggedException = Assert.Single(exceptionLogger.LoggedExceptions);
        Assert.Equal($"Jobs.{nameof(SendGeneratedPasswordEmailJob)}", loggedException.Source);
        Assert.DoesNotContain(PlainPassword, loggedException.Exception.Message);
        Assert.DoesNotContain(encrypted, loggedException.Exception.Message);

        AssertNoLogMentionsPassword(logger, encrypted);
    }

    private static void AssertNoLogMentionsPassword(CapturingLogger<SendGeneratedPasswordEmailJob> logger, string encrypted)
    {
        foreach (var entry in logger.Entries)
        {
            Assert.DoesNotContain(PlainPassword, entry.Message);
            Assert.DoesNotContain(encrypted, entry.Message);

            foreach (var value in entry.Properties.Values)
            {
                var text = value?.ToString() ?? string.Empty;
                Assert.DoesNotContain(PlainPassword, text);
                Assert.DoesNotContain(encrypted, text);
            }
        }
    }
}
