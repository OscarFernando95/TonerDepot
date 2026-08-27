namespace Toner.Infrastructure.Security;

// Mismo patrón que JwtSettingsValidator: falla el arranque con un mensaje explícito en vez de dejar
// que la app arranque y reviente recién al primer intento de cifrar/descifrar.
public static class JobPayloadEncryptionSettingsValidator
{
    private const int RequiredKeyBytes = 32; // AES-256

    public static void EnsureValid(JobPayloadEncryptionSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Key))
        {
            throw new InvalidOperationException(
                "Falta la clave de configuración 'JobPayloadEncryption:Key'. Generá una clave AES-256 " +
                "(32 bytes en Base64, ej. con 'openssl rand -base64 32') antes de arrancar la aplicación.");
        }

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(settings.Key);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "'JobPayloadEncryption:Key' no es Base64 válido. Generá una clave con " +
                "'openssl rand -base64 32'.");
        }

        if (keyBytes.Length != RequiredKeyBytes)
        {
            throw new InvalidOperationException(
                $"'JobPayloadEncryption:Key' debe decodificar a {RequiredKeyBytes} bytes (AES-256). " +
                $"Longitud actual: {keyBytes.Length} bytes.");
        }
    }
}
