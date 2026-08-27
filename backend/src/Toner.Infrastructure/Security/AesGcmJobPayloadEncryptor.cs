using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Toner.Infrastructure.Security;

// AES-256-GCM: cifrado autenticado en una sola primitiva (a diferencia de CBC, no necesita un HMAC
// aparte ni expone la superficie de un ataque de padding oracle). Verificado que corre en este
// entorno (.NET 8) antes de comprometerse a la librería, no asumido.
//
// Formato de salida: Base64(nonce (12 bytes) ‖ tag (16 bytes) ‖ ciphertext). Nonce aleatorio en cada
// llamada — nunca reutilizado, que es el único requisito real de seguridad de GCM.
public class AesGcmJobPayloadEncryptor : IJobPayloadEncryptor
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesGcmJobPayloadEncryptor(IOptions<JobPayloadEncryptionSettings> settings)
    {
        _key = Convert.FromBase64String(settings.Value.Key);
    }

    public string Encrypt(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintextBytes.Length];

        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }

        var output = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, output, NonceSize + TagSize, ciphertext.Length);

        return Convert.ToBase64String(output);
    }

    public string Decrypt(string ciphertext)
    {
        var input = Convert.FromBase64String(ciphertext);

        var nonce = input.AsSpan(0, NonceSize);
        var tag = input.AsSpan(NonceSize, TagSize);
        var encrypted = input.AsSpan(NonceSize + TagSize);

        var plaintextBytes = new byte[encrypted.Length];

        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Decrypt(nonce, encrypted, tag, plaintextBytes);
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
