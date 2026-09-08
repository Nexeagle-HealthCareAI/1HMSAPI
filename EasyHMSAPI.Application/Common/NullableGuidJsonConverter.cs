using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.Common
{
    // System.Text.Json's built-in Guid converter rejects "" for a nullable Guid (throws instead of
    // treating it like omitted/null) — several frontend forms send "" for an unselected dropdown.
    public class NullableGuidJsonConverter : JsonConverter<Guid?>
    {
        public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            var value = reader.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value);
        }

        public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
