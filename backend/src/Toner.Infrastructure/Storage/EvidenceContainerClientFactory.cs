using Azure.Identity;
using Azure.Storage.Blobs;

namespace Toner.Infrastructure.Storage;

// Único punto que construye el cliente del contenedor de evidencias: lo usan AzureBlobEvidenceStorage y
// BlobStorageHealthCheck, así el health check comprueba exactamente la misma credencial que usa la app.
// Llamar después de EvidenceStorageSettingsValidator.EnsureValid.
public static class EvidenceContainerClientFactory
{
    public static BlobContainerClient Create(EvidenceStorageSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            // Azurite en desarrollo.
            return new BlobContainerClient(settings.ConnectionString, settings.ContainerName);
        }

        // Azure: Managed Identity (AUDIT.md H-18). En App Service, DefaultAzureCredential resuelve la identidad
        // asignada a la app; no hay ningún secreto en la configuración.
        var containerUri = new Uri($"{settings.ServiceUri!.TrimEnd('/')}/{settings.ContainerName}");
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = string.IsNullOrWhiteSpace(settings.ManagedIdentityClientId) ? null : settings.ManagedIdentityClientId
        });
        return new BlobContainerClient(containerUri, credential);
    }
}
