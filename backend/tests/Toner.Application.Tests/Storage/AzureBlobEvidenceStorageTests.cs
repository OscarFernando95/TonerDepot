using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Toner.Infrastructure.Storage;

namespace Toner.Application.Tests.Storage;

// AUDIT.md H-35: _containerReady era un bool sin sincronización en un singleton. Sin Azurite: el cliente
// de contenedor es un doble que cuenta las llamadas a CreateIfNotExistsAsync.
public class AzureBlobEvidenceStorageTests
{
    private sealed class FakeContainer : BlobContainerClient
    {
        private int _failuresLeft;
        public int CreateCalls;

        public FakeContainer(int failFirst = 0) { _failuresLeft = failFirst; }

        public override async Task<Response<BlobContainerInfo>> CreateIfNotExistsAsync(
            PublicAccessType publicAccessType = PublicAccessType.None,
            IDictionary<string, string>? metadata = null,
            BlobContainerEncryptionScopeOptions? encryptionScopeOptions = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref CreateCalls);
            await Task.Delay(20, cancellationToken); // ventana para que los hilos concurrentes se pisen
            if (Interlocked.Decrement(ref _failuresLeft) >= 0)
            {
                throw new RequestFailedException("Blob caído (simulado).");
            }

            return null!;
        }
    }

    [Fact]
    public async Task EnsureContainer_ConLlamadasConcurrentes_CreaElContenedorUnaSolaVez()
    {
        var container = new FakeContainer();
        var storage = new AzureBlobEvidenceStorage(container);

        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => storage.EnsureContainerAsync()));
        await storage.EnsureContainerAsync();

        Assert.Equal(1, container.CreateCalls);
    }

    [Fact]
    public async Task EnsureContainer_UnFalloNoSeRecuerda_ElSiguienteIntentoReintenta()
    {
        var container = new FakeContainer(failFirst: 1);
        var storage = new AzureBlobEvidenceStorage(container);

        await Assert.ThrowsAsync<RequestFailedException>(() => storage.EnsureContainerAsync());
        await storage.EnsureContainerAsync();
        await storage.EnsureContainerAsync();

        Assert.Equal(2, container.CreateCalls);
    }

    [Fact]
    public async Task Initializer_SiBlobEstaCaido_NoImpideArrancar()
    {
        var container = new FakeContainer(failFirst: 5);
        var initializer = new EvidenceStorageInitializer(
            new AzureBlobEvidenceStorage(container),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EvidenceStorageInitializer>.Instance);

        await initializer.StartAsync(CancellationToken.None); // no debe lanzar

        Assert.Equal(1, container.CreateCalls);
    }
}
