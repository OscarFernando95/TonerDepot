using Toner.Infrastructure.Auth;

namespace Toner.Application.Tests.Auth;

// Verifica el hallazgo #19 de SECURITY_AUDIT.md: la app no debe arrancar con una clave de firma JWT
// débil en silencio. JwtTokenGenerator firma con HmacSha256, que por RFC 7518 exige 256 bits (32
// bytes) como mínimo.
public class JwtSettingsValidatorTests
{
    private static JwtSettings SettingsWithKey(string signingKey) => new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = signingKey,
        ExpiryMinutes = 60
    };

    [Fact]
    public void EnsureValid_ClaveDeMenosDe32Bytes_Lanza()
    {
        // 31 caracteres ASCII = 31 bytes UTF-8, uno menos del mínimo.
        var settings = SettingsWithKey(new string('a', 31));

        var ex = Assert.Throws<InvalidOperationException>(() => JwtSettingsValidator.EnsureValid(settings));
        Assert.Contains("32", ex.Message);
    }

    [Fact]
    public void EnsureValid_ClaveDeExactamente32Bytes_NoLanza()
    {
        var settings = SettingsWithKey(new string('a', 32));

        var exception = Record.Exception(() => JwtSettingsValidator.EnsureValid(settings));

        Assert.Null(exception);
    }
}
