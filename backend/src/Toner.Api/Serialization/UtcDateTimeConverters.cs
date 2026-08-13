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
    public static DateTime Parse(string raw) =>
        DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal).UtcDateTime;
}

public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        UtcDateTimeParsing.Parse(reader.GetString()!);

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

        return UtcDateTimeParsing.Parse(reader.GetString()!);
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
