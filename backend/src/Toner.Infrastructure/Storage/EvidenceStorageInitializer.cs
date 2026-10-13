using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Toner.Infrastructure.Storage;

// Crea el contenedor de evidencias al arrancar, para que el primer check-in con foto de un técnico no
// pague (ni dependa de) la creación. Un fallo NO impide arrancar: con Blob caído la app debe seguir
// sirviendo el resto, y AzureBlobEvidenceStorage.SaveAsync reintenta la creación en el primer guardado.
public class EvidenceStorageInitializer : IHostedService
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(10);

    private readonly AzureBlobEvidenceStorage _storage;
    private readonly ILogger<EvidenceStorageInitializer> _logger;

    public EvidenceStorageInitializer(AzureBlobEvidenceStorage storage, ILogger<EvidenceStorageInitializer> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(StartupTimeout);

        try
        {
            await _storage.EnsureContainerAsync(timeout.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "No se pudo crear/verificar el contenedor de evidencias al arrancar; se reintentará en el primer guardado.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
