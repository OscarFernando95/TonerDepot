using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Toner.Infrastructure.Storage;

namespace Toner.Infrastructure.Health;

// Sin Blob, el check-in/check-out con foto de los técnicos falla aunque Postgres esté sano. ExistsAsync
// no lanza si el contenedor aún no existe (404): solo falla si la cuenta no es alcanzable o rechaza las
// credenciales, que es lo que interesa comprobar aquí.
public class BlobStorageHealthCheck : CachedHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly BlobContainerClient _container;

    public BlobStorageHealthCheck(IOptions<EvidenceStorageSettings> options, TimeSpan? ttl = null, TimeProvider? time = null)
        : base(ttl, time)
    {
        var settings = options.Value;
        _container = new BlobContainerClient(settings.ConnectionString, settings.ContainerName);
    }

    protected override async Task<HealthCheckResult> CheckCoreAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            await _container.ExistsAsync(timeout.Token);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Descripción genérica a propósito: el cuerpo de la respuesta es público.
            return new HealthCheckResult(context.Registration.FailureStatus, "No se pudo contactar a Blob Storage.", ex);
        }
    }
}
