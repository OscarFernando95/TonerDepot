using Toner.Infrastructure.Security;

namespace Toner.Application.Tests.Auth;

// Verifica el hallazgo #3 de SECURITY_AUDIT.md: sin esto, la app arrancaría sin poder cifrar la
// contraseña generada antes de encolarla, o peor, con una clave inválida que solo se nota al primer
// intento real de cifrar.
public class JobPayloadEncryptionSettingsValidatorTests
{
    [Fact]
    public void EnsureValid_ClaveVacia_Lanza()
    {
        var settings = new JobPayloadEncryptionSettings { Key = "" };

        var ex = Assert.Throws<InvalidOperationException>(() => JobPayloadEncryptionSettingsValidator.EnsureValid(settings));
        Assert.Contains("JobPayloadEncryption:Key", ex.Message);
    }

    [Fact]
    public void EnsureValid_NoEsBase64Valido_Lanza()
    {
        var settings = new JobPayloadEncryptionSettings { Key = "esto no es base64!!" };

        Assert.Throws<InvalidOperationException>(() => JobPayloadEncryptionSettingsValidator.EnsureValid(settings));
    }

    [Fact]
    public void EnsureValid_ClaveDeMenosDe32Bytes_Lanza()
    {
        // 16 bytes válidos en Base64, pero no los 32 (AES-256) que exige el validador.
        var settings = new JobPayloadEncryptionSettings { Key = Convert.ToBase64String(new byte[16]) };

        var ex = Assert.Throws<InvalidOperationException>(() => JobPayloadEncryptionSettingsValidator.EnsureValid(settings));
        Assert.Contains("32", ex.Message);
    }

    [Fact]
    public void EnsureValid_ClaveDeExactamente32Bytes_NoLanza()
    {
        var settings = new JobPayloadEncryptionSettings { Key = Convert.ToBase64String(new byte[32]) };

        var exception = Record.Exception(() => JobPayloadEncryptionSettingsValidator.EnsureValid(settings));

        Assert.Null(exception);
    }
}
