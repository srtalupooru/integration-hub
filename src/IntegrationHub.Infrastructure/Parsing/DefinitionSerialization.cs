using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using IntegrationHub.Application;
using IntegrationHub.Domain;
using YamlDotNet.Serialization;
namespace IntegrationHub.Infrastructure.Parsing;

public static class CanonicalJson
{
    public static JsonSerializerOptions Options { get; } = Create();
    private static JsonSerializerOptions Create()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            foreach (var property in info.Properties)
            {
                if (info.Type == typeof(IntegrationDefinition) && property.Name == "businessDomain") property.Name = "domain";
                if (info.Type == typeof(IntegrationDefinition) && property.Name == "allMessages") property.ShouldSerialize = (_, _) => false;
                if (info.Type == typeof(IntegrationEdge) && property.Name == "fromNodeId") property.Name = "from";
                if (info.Type == typeof(IntegrationEdge) && property.Name == "toNodeId") property.Name = "to";
            }
        });
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = resolver, WriteIndented = true, MaxDepth = 64, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.Converters.Add(new IntegrationIdConverter());
        return options;
    }
    public sealed class IntegrationIdConverter : JsonConverter<IntegrationId>
    {
        public override IntegrationId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => new(reader.GetString()!);
        public override void Write(Utf8JsonWriter writer, IntegrationId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
    }
}
public sealed class DefinitionSerializer : IDefinitionSerializer
{
    public string Serialize(IntegrationDefinition definition, string format)
    {
        var json = JsonSerializer.Serialize(new { integration = definition }, CanonicalJson.Options);
        if (format == "json") return json;
        if (format is not ("yaml" or "yml")) throw new ArgumentException("Format must be json or yaml.");
        using var document = JsonDocument.Parse(json);
        return new SerializerBuilder().DisableAliases().WithQuotingNecessaryStrings().Build().Serialize(ToObject(document.RootElement));
    }
    private static object? ToObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ToObject(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(ToObject).ToArray(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => element.GetDecimal(),
        _ => null
    };
}
