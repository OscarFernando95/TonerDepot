using System.Security.Cryptography;
using Toner.Domain.Common;

namespace Toner.Application.Auth;

// Genera la contraseña de un usuario nuevo o reseteado (hallazgo #3 de SECURITY_AUDIT.md — antes era
// la constante fija PasswordDefaults.DefaultPassword para todo el mundo). Cumple
// ChangePasswordRequestValidator por CONSTRUCCIÓN, no por regex duplicada: garantiza al menos un
// carácter de cada clase exigida en vez de generar al azar y esperar que coincida.
public static class SecurePasswordGenerator
{
    // Más larga que el mínimo exigido (PasswordDefaults.MinimumLength = 10): es una contraseña que
    // nadie va a memorizar y se reemplaza en el primer login, así que no hay costo de usabilidad en
    // darle más entropía que el piso legal.
    private const int Length = 16;

    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // sin I, O: ambiguos al transcribir
    private const string Lowercase = "abcdefghijkmnpqrstuvwxyz"; // sin l, o
    private const string Digits = "23456789"; // sin 0, 1
    private const string AllChars = Uppercase + Lowercase + Digits;

    public static string Generate()
    {
        var chars = new char[Length];

        // Un carácter garantizado de cada clase que exige ChangePasswordRequestValidator — esto es lo
        // que hace que el resultado cumpla la política siempre, no en la mayoría de los casos.
        chars[0] = PickFrom(Uppercase);
        chars[1] = PickFrom(Lowercase);
        chars[2] = PickFrom(Digits);

        for (var i = 3; i < Length; i++)
        {
            chars[i] = PickFrom(AllChars);
        }

        Shuffle(chars);

        return new string(chars);
    }

    private static char PickFrom(string pool) => pool[RandomNumberGenerator.GetInt32(pool.Length)];

    // Fisher-Yates con RNG criptográfico: sin esto, las posiciones 0-2 tendrían siempre una
    // mayúscula/minúscula/dígito garantizados en el mismo lugar, un patrón que no debería existir en
    // algo que se llama "aleatorio".
    private static void Shuffle(char[] chars)
    {
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }
}
