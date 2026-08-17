using System.Text;

namespace Toner.Infrastructure.Auth;

// Extraído de Program.cs para poder testearlo directamente, sin tener que arrancar todo el host
// (mismo patrón que Toner.Api.Auth.SecurityStampValidator).
public static class JwtSettingsValidator
{
    // JwtTokenGenerator firma con HmacSha256 — RFC 7518 exige una clave de al menos 256 bits
    // (32 bytes) para ese algoritmo. Menos que eso debilita la firma sin que nada lo avise
    // (SECURITY_AUDIT.md hallazgo #19).
    private const int MinimumSigningKeyBytes = 32;

    public static void EnsureValid(JwtSettings settings)
    {
        var actualBytes = Encoding.UTF8.GetByteCount(settings.SigningKey);

        if (actualBytes < MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey debe tener al menos {MinimumSigningKeyBytes} bytes (256 bits) para " +
                $"HMAC-SHA256. Longitud actual: {actualBytes} bytes. Generá una clave más larga antes " +
                "de arrancar la aplicación.");
        }
    }
}
