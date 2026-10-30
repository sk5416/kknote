using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KkNote.Models;

public sealed class DateOnlyJsonConverter : JsonConverter<DateTime?>
{
    private const string Pattern = "yyyy-MM-dd";

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType != JsonTokenType.String)
            return null;
        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (DateTime.TryParseExact(text, Pattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date.Date;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return date.Date;
        return null;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Value.ToString(Pattern, CultureInfo.InvariantCulture));
    }
}
