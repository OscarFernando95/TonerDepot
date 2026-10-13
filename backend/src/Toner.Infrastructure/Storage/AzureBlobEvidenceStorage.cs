using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Toner.Application.Evidences;

namespace Toner.Infrastructure.Storage;

// Guarda las fotos en un contenedor PRIVADO de Azure Blob Storage (Azurite en desarrollo). Nadie accede
// al blob directamente: el contenido se sirve por GET /api/evidence/{id}/content, que autoriza primero.
public class AzureBlobEvidenceStorage : IEvidenceStorage
{
    private readonly BlobContainerClient _container;
    private readonly SemaphoreSlim _containerGate = new(1, 1);
    private volatile bool _containerReady;

    public AzureBlobEvidenceStorage(IOptions<EvidenceStorageSettings> options)
        : this(new BlobContainerClient(options.Value.ConnectionString, options.Value.ContainerName))
    {
    }

    // Para pruebas: permite inyectar un cliente de contenedor falso.
    public AzureBlobEvidenceStorage(BlobContainerClient container)
    {
        _container = container;
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        await _container.GetBlobClient(key).UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            cancellationToken);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _container.GetBlobClient(key).OpenReadAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        await _container.GetBlobClient(key).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    // Idempotente y seguro entre hilos. Lo llama el arranque (EvidenceStorageInitializer) y, como red de
    // seguridad, SaveAsync: si Blob estaba caído al arrancar, el primer guardado crea el contenedor.
    // Un fallo NO se recuerda: el siguiente intento vuelve a probar (AUDIT.md H-35).
    public async Task EnsureContainerAsync(CancellationToken cancellationToken = default)
    {
        if (_containerReady)
        {
            return;
        }

        await _containerGate.WaitAsync(cancellationToken);
        try
        {
            if (_containerReady)
            {
                return;
            }

            // Sin PublicAccessType: el contenedor es privado.
            await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            _containerReady = true;
        }
        finally
        {
            _containerGate.Release();
        }
    }
}
