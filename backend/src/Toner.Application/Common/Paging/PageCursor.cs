using System.Text;
using FluentValidation;
using FluentValidation.Results;

namespace Toner.Application.Common.Paging;

// Cursor de paginación keyset: la tupla (marca de tiempo, Id) de la última fila entregada.
//
// Por qué la tupla y no solo la fecha: varias filas pueden compartir instante exacto — todo lo que
// se crea dentro de un mismo SaveChanges lo hace — y con solo la fecha el cursor saltearía o
// repetiría filas en ese empate. El Id desempata de forma determinista, y por eso los listados con
// cursor ordenan por (fecha DESC, Id DESC).
//
// Se codifica en base64url para que sea opaco: el cliente lo trata como un token que devuelve tal
// cual, sin depender de su forma interna, que así puede cambiar sin romper el contrato.
public static class PageCursor
{
    public static string Encode(DateTime timestamp, Guid id)
    {
        var raw = Encoding.UTF8.GetBytes($"{timestamp.Ticks}|{id}");
        return Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    // null si no viene cursor (primera página). Un cursor presente pero corrupto es un error del
    // cliente, no del servidor: se traduce a ValidationException, que el middleware mapea a 400.
    public static (DateTime Timestamp, Guid Id)? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var normalized = cursor.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + ((4 - (normalized.Length % 4)) % 4), '=');

            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(normalized)).Split('|');
            if (parts.Length == 2
                && long.TryParse(parts[0], out var ticks)
                && Guid.TryParse(parts[1], out var id)
                && ticks is >= 0 and <= 3_155_378_975_999_999_999) // rango válido de DateTime.Ticks
            {
                return (new DateTime(ticks, DateTimeKind.Utc), id);
            }
        }
        catch (FormatException)
        {
            // Cae al throw de abajo: base64 inválido es el mismo error de cliente.
        }

        throw new ValidationException(new[]
        {
            new ValidationFailure("cursor", "El cursor de paginación no es válido. Usa el valor 'nextCursor' que devolvió la página anterior.")
        });
    }
}
