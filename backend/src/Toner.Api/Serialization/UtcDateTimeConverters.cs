using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Toner.Api.Serialization;

// Npgsql exige Kind=Utc para escribir en columnas timestamptz. El frontend ya envía ISO con 'Z'
// (convierte sus date-pickers con toISOString() antes de mandar la request), pero cualquier otro
// cliente (Swagger, curl, la futura app móvil) puede mandar una fecha sin offset ("2026-08-13") y
// esa request explotaba con un 500 genérico en vez de un error claro o un resultado correcto.
// AssumeUniversal trata las fechas sin offset como si ya fueran UTC (mismo criterio que usa el
// frontend); AdjustToUniversal normaliza las que sí traen un offset explícito.
internal static class UtcDateTimeParsing
{
    // Cualquier fallo de parseo se relanza como JsonException a propósito: es la única excepción que
    // el SystemTextJsonInputFormatter de ASP.NET Core intercepta para devolver un 400 con detalle de
    // campo automáticamente (antes de llegar al controller). FormatException/InvalidOperationException/
    // ArgumentNullException "crudas" no las reconoce nadie y terminaban como 500 genérico.
    public static DateTime Parse(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Se esperaba una fecha en formato texto (ISO 8601), por ejemplo '2026-08-13' o '2026-08-13T00:00:00Z'.");
        }

        var raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new JsonException("La fecha no puede estar vacía.");
        }

        try
        {
            return DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal).UtcDateTime;
        }
        catch (FormatException ex)
        {
            throw new JsonException($"'{raw}' no es una fecha válida.", ex);
        }
    }
}

public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        UtcDateTimeParsing.Parse(ref reader);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

public class UtcNullableDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return UtcDateTimeParsing.Parse(ref reader);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
        }
    }
}
