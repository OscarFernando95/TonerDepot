using Hangfire;
using Toner.Application.Common.Interfaces;
using Toner.Infrastructure.Security;

namespace Toner.Infrastructure.Jobs;

public class HangfireBackgroundJobScheduler : IBackgroundJobScheduler
{
    private readonly IBackgroundJobClient _client;
    private readonly IJobPayloadEncryptor _encryptor;

    public HangfireBackgroundJobScheduler(IBackgroundJobClient client, IJobPayloadEncryptor encryptor)
    {
        _client = client;
        _encryptor = encryptor;
    }

    public void EnqueueGeneratedPasswordEmail(string cedula, string generatedPassword)
    {
        // Cifrado ACÁ, antes de que Hangfire serialice el argumento a sus propias tablas de Postgres
        // (independientes de RLS) — es el único punto en el que la contraseña en claro cruza hacia un
        // almacenamiento durable que Application no controla.
        var encryptedPassword = _encryptor.Encrypt(generatedPassword);

        _client.Enqueue<SendGeneratedPasswordEmailJob>(
            job => job.RunAsync(cedula, encryptedPassword, CancellationToken.None));
    }
}
