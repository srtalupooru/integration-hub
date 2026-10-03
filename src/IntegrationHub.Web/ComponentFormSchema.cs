using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace IntegrationHub.Web;

// Keep the draft as JSON so edits to one field preserve all other schema-supported fields.
public static class ComponentFormSchema
{
    public static JsonObject NewDocument() => JsonNode.Parse("""
        {"schemaVersion":"1.1","component":{"id":"","name":"","type":"Api","environment":"development","status":"Draft"}}
        """)!.AsObject();
    public static JsonObject Resolve(JsonObject root, JsonObject schema)
    {
        if (schema["$ref"] is JsonValue reference)
        {
            var path = reference.GetValue<string>().Split('/');
            schema = root[path[1]]![path[2]]!.AsObject();
        }
        if (schema["oneOf"] is JsonArray variants)
            return variants.Select(v => v!.AsObject()).First(v => Text(v["type"]) == "array");
        return schema;
    }
    public static string Text(JsonNode? node) => node is null ? "" : node is JsonValue v && v.TryGetValue<string>(out var text) ? text : node.ToJsonString();
    public static string Kind(JsonObject schema) => schema.ContainsKey("enum") || schema.ContainsKey("const") ? "string" : Text(schema["type"]);
    public static bool Required(JsonObject schema, string name) => schema["required"] is JsonArray required && required.Any(v => Text(v) == name);
    public static bool Required(JsonObject schema, JsonObject parent, string name, string path) => Required(schema, name) || (name switch
    {
        "subscription" => (Text(parent["action"]) == "consumes" || path.StartsWith("/component/consumes/", StringComparison.Ordinal)) && Text(parent["channel"]?["kind"]) is "Topic" or "Stream",
        "messageProperty" => Text(parent["mode"]) == "Property",
        "header" => Text(parent["mode"]) == "Header",
        "description" => Text(parent["mode"]) == "Custom",
        "notFoundDescription" => Text(parent["whenNotFound"]) == "Custom",
        "correlationProperty" => parent["handles"] is JsonArray rules && rules.OfType<JsonObject>().Any(r => Text(r["correlation"]?["mode"]) is "Property" or "Header"),
        _ => false
    });
    public static string Label(string name) => name switch
    {
        "id" => "ID", "type" => "Component type", "action" => "Message action", "contract" => "Message contract",
        "namespace" => "Broker namespace", "name" => "Name", "message" => "Consumed message ID", "outputs" => "Outgoing message IDs",
        "targetComponent" => "Target component ID", "endpoint" => "Endpoint", "schemaFingerprint" => "Schema fingerprint",
        "technology" => "Technologies", "processing" => "NServiceBus processing", "calls" => "HTTP calls",
        "requestsTimeouts" => "Timeout IDs to request", "dataType" => "Saga state type", "implementation" => "Implementation class",
        _ => char.ToUpperInvariant(name[0]) + Regex.Replace(name[1..], "([A-Z])", " $1").ToLowerInvariant()
    };
    public static string Help(string name) => name switch
    {
        "id" => "Lowercase letters, numbers, dots, underscores, or hyphens. IDs must be unique in their scope.",
        "environment" => "For example: development, test, production. Connections only match in the same environment.",
        "contract" => "Exact message name used by your application, such as InvoiceCreated or ProcessInvoice. Dots are optional. Use identical spelling, casing, and version on both components.",
        "action" => "Publish an event, send a command, or consume an incoming message.",
        "subscription" => "Required for consumed topics and streams. For a stream, enter the consumer group.",
        "namespace" => "The broker namespace or cluster shared by the sender and receiver.",
        "path" => "HTTP path starting with /, for example /invoices/{id}.",
        "targetComponent" => "The ID of the component exposing this HTTP operation.",
        "message" => "An ID from this component’s consumed messages.",
        "outputs" => "IDs from this component’s published events or sent commands. These are possible outputs.",
        "requestsTimeouts" => "Timeout IDs declared inside this same saga.",
        "correlationProperty" => "The saga’s business key, for example InvoiceId. Do not use its internal Id.",
        "delay" => "Positive ISO 8601 duration, for example PT30M. Use delay or schedule, not both.",
        "schedule" => "Describe dynamic timeout scheduling instead of entering a fixed delay.",
        "schemaFingerprint" => "Optional SHA-256: exactly 64 lowercase hexadecimal characters.",
        "startsSaga" => "This message may start a new saga or continue an existing instance.",
        "completesSaga" => "This rule may complete the saga, subject to its condition.",
        "outbox" => "Document the endpoint configuration. Leave unspecified if unknown.",
        _ => ""
    };
    public static JsonNode Create(JsonObject root, JsonObject schema, string name = "")
    {
        schema = Resolve(root, schema);
        if (schema["const"] is JsonNode constant) return constant.DeepClone();
        if (schema["enum"] is JsonArray options) return options[0]!.DeepClone();
        if (Kind(schema) == "array") return new JsonArray();
        if (Kind(schema) == "object")
        {
            var result = new JsonObject();
            foreach (var property in schema["properties"]!.AsObject().Where(p => Required(schema, p.Key)))
                result[property.Key] = Create(root, property.Value!.AsObject(), property.Key);
            return result;
        }
        if (Kind(schema) == "boolean") return JsonValue.Create(false)!;
        return JsonValue.Create(name switch { "version" => "1.0", "contentType" => "application/json", _ => "" })!;
    }
    public static void Set(JsonObject parent, string name, string value, JsonObject schema, bool required)
    {
        if (value.Length == 0 && !required) parent.Remove(name);
        else parent[name] = Kind(schema) == "boolean" ? JsonValue.Create(value == "true") : JsonValue.Create(value);
        if (name == "action")
        {
            if (value == "publishes") parent["messageType"] = "Event";
            if (value == "sends") parent["messageType"] = "Command";
            // A hidden value must not be silently discarded when changing a control.
            // Inapplicable existing values remain visible for explicit removal and validation.
        }
    }
    public static bool Visible(JsonObject parent, string name)
    {
        if (parent[name] is not null) return true;
        if (name == "subscription" && parent.ContainsKey("action"))
            return Text(parent["action"]) == "consumes" && Text(parent["channel"]?["kind"]) is "Topic" or "Stream";
        if (name == "messageProperty" && parent.ContainsKey("mode")) return Text(parent["mode"]) == "Property";
        if (name == "header" && parent.ContainsKey("mode")) return Text(parent["mode"]) == "Header";
        return true;
    }
    public static bool HasLegacy(JsonObject component) => new[] { "consumes", "publishes", "sends" }.Any(n => component[n] is JsonArray { Count: > 0 });
    public static string Serialize(JsonObject document)
    {
        var copy = document.DeepClone().AsObject();
        var component = copy["component"]!.AsObject();
        if (HasLegacy(component))
        {
            if (component["messages"] is not JsonArray { Count: > 0 }) component.Remove("messages");
        }
        else foreach (var name in new[] { "consumes", "publishes", "sends" }) component.Remove(name);
        return copy.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
    public static string ItemTitle(JsonNode? item, int index) => item is JsonObject o
        ? new[] { "name", "contract", "id", "message", "path" }.Select(n => Text(o[n])).FirstOrDefault(s => s.Length > 0) ?? $"Item {index + 1}"
        : $"Item {index + 1}";
}
