namespace Toner.Infrastructure.Storage;

// Exactamente UNA de las dos formas de acceso (AUDIT.md H-18):
//   - ConnectionString: Azurite en desarrollo. Nunca versionada (CLAUDE.md, Secretos): appsettings.Development.json o
//     EvidenceStorage__ConnectionString. En Azure equivale a la CLAVE de la cuenta (acceso total): no usarla allí.
//   - ServiceUri (https://<cuenta>.blob.core.windows.net): Azure, con Managed Identity vía DefaultAzureCredential —
//     sin secreto, permiso acotado con el rol "Storage Blob Data Contributor". ManagedIdentityClientId solo si la
//     identidad es user-assigned. Ver el checklist de Azure en el README.
public class EvidenceStorageSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "evidence";
    public string? ServiceUri { get; set; }
    public string? ManagedIdentityClientId { get; set; }
}

// Falla rápido al arrancar si la configuración no permite guardar las fotos: sin esto el check-in de los técnicos
// empezaría a fallar en runtime.
public static class EvidenceStorageSettingsValidator
{
    public static void EnsureValid(EvidenceStorageSettings settings)
    {
        var hasConnectionString = !string.IsNullOrWhiteSpace(settings.ConnectionString);
        var hasServiceUri = !string.IsNullOrWhiteSpace(settings.ServiceUri);

        if (!hasConnectionString && !hasServiceUri)
        {
            throw new InvalidOperationException(
                "Falta la configuración de Blob Storage para las fotos de evidencia: define EvidenceStorage:ServiceUri " +
                "(Azure, con Managed Identity) o EvidenceStorage:ConnectionString (Azurite en desarrollo), por configuración " +
                "o variable de entorno (EvidenceStorage__ServiceUri / EvidenceStorage__ConnectionString).");
        }

        if (hasConnectionString && hasServiceUri)
        {
            throw new InvalidOperationException(
                "EvidenceStorage:ConnectionString y EvidenceStorage:ServiceUri no pueden ir juntas: sería ambiguo qué credencial " +
                "se usa. En Azure deja solo ServiceUri (Managed Identity); en desarrollo, solo ConnectionString.");
        }

        if (hasServiceUri
            && (!Uri.TryCreate(settings.ServiceUri, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"EvidenceStorage:ServiceUri debe ser una URL https absoluta (https://<cuenta>.blob.core.windows.net). Valor: '{settings.ServiceUri}'.");
        }

        if (string.IsNullOrWhiteSpace(settings.ContainerName))
        {
            throw new InvalidOperationException("EvidenceStorage:ContainerName no puede estar vacío.");
        }
    }
}
