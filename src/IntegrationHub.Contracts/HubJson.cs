using System.Text.Json;
using System.Text.Json.Serialization;
using IntegrationHub.Domain;
namespace IntegrationHub.Contracts;
public static class HubJson
{
    public static JsonSerializerOptions Options { get; } = Create();
    private static JsonSerializerOptions Create() { var options = new JsonSerializerOptions(JsonSerializerDefaults.Web); Configure(options); return options; }
    public static void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.Converters.Add(new IdConverter());
    }
    private sealed class IdConverter : JsonConverter<IntegrationId>
    {
        public override IntegrationId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => new(reader.GetString()!);
        public override void Write(Utf8JsonWriter writer, IntegrationId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
    }
}
