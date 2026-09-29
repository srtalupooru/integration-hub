using System.Text.Json;
using System.Text.Json.Serialization;
namespace IntegrationHub.Domain;

// Read existing component JSON without rewriting saved definitions or history.
// Only the component technology property uses this compatibility converter.
public sealed class TechnologyListJsonConverter : JsonConverter<IReadOnlyList<string>>
{
    public override bool HandleNull => true;

    public override IReadOnlyList<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var legacy = reader.GetString()!;
            return string.IsNullOrWhiteSpace(legacy) ? [] : [legacy.Trim()];
        }
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Technology must be a list of strings.");
        var items = new List<string>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray) return items;
            if (reader.TokenType != JsonTokenType.String) throw new JsonException("Technology entries must be strings.");
            items.Add(reader.GetString()!);
        }
        throw new JsonException("Incomplete technology list.");
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value) writer.WriteStringValue(item);
        writer.WriteEndArray();
    }
}
