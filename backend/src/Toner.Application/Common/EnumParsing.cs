using FluentValidation;
using FluentValidation.Results;

namespace Toner.Application.Common;

// Defensa en profundidad para servicios invocables sin pasar por FluentValidation (tests, otro
// servicio a futuro) — mismo criterio que ClientService.CreateAsync con Locations. Sin esto, un
// Enum.Parse crudo sobre un string que no llegó a validarse lanza ArgumentException, que
// ExceptionHandlingMiddleware mapea a 500 en vez de al 400 que le corresponde a un dato de entrada
// inválido (CODE_QUALITY_AUDIT.md hallazgo #20).
internal static class EnumParsing
{
    public static TEnum ParseOrThrow<TEnum>(string? value, string fieldName) where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new ValidationException(new[]
        {
            new ValidationFailure(fieldName, $"{fieldName} debe ser uno de: {string.Join(", ", Enum.GetNames<TEnum>())}.")
        });
    }
}
