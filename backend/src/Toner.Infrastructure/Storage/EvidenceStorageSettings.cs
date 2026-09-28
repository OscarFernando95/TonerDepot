namespace Toner.Infrastructure.Storage;

public class EvidenceStorageSettings
{
    // Nunca versionada (ver CLAUDE.md, Secretos): en desarrollo va en appsettings.Development.json o en la
    // variable EvidenceStorage__ConnectionString; en producción, la cadena de la cuenta de Azure Storage.
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "evidence";
}

// Falla rápido al arrancar si falta la cadena: sin ella las fotos de evidencia no se podrían guardar y el
// check-in de los técnicos empezaría a fallar en runtime.
public static class EvidenceStorageSettingsValidator
{
    public static void EnsureValid(EvidenceStorageSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new InvalidOperationException(
                "Falta EvidenceStorage:ConnectionString. Las fotos de evidencia se guardan en Azure Blob Storage " +
                "(Azurite en desarrollo): define la cadena por configuración o variable de entorno " +
                "(EvidenceStorage__ConnectionString) antes de arrancar la aplicación.");
        }

        if (string.IsNullOrWhiteSpace(settings.ContainerName))
        {
            throw new InvalidOperationException("EvidenceStorage:ContainerName no puede estar vacío.");
        }
    }
}
