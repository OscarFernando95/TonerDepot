using Microsoft.Extensions.Options;
using Toner.Infrastructure.Security;

namespace Toner.Application.Tests.Auth;

public class AesGcmJobPayloadEncryptorTests
{
    private static AesGcmJobPayloadEncryptor BuildEncryptor(byte[]? key = null) =>
        new(Options.Create(new JobPayloadEncryptionSettings { Key = Convert.ToBase64String(key ?? new byte[32]) }));

    [Fact]
    public void Encrypt_LuegoDecrypt_DevuelveElOriginal()
    {
        var encryptor = BuildEncryptor();

        var ciphertext = encryptor.Encrypt("Passw0rdSegura!");

        Assert.Equal("Passw0rdSegura!", encryptor.Decrypt(ciphertext));
    }

    [Fact]
    public void Encrypt_MismoTextoDosVeces_DaCiphertextsDistintos()
    {
        // El nonce es aleatorio por llamada — si esto fallara, estaría reutilizando el nonce, que es
        // el único requisito real de seguridad de GCM.
        var encryptor = BuildEncryptor();

        var a = encryptor.Encrypt("Passw0rdSegura!");
        var b = encryptor.Encrypt("Passw0rdSegura!");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Decrypt_ConClaveDistinta_Falla()
    {
        var ciphertext = BuildEncryptor(new byte[32]).Encrypt("Passw0rdSegura!");

        var otherKey = new byte[32];
        otherKey[0] = 1; // distinta de la clave de todo-ceros usada para cifrar
        var encryptorConOtraClave = BuildEncryptor(otherKey);

        Assert.ThrowsAny<Exception>(() => encryptorConOtraClave.Decrypt(ciphertext));
    }
}
