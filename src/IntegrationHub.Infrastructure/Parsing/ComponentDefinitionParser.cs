using System.Text.Json;
using System.Text.Json.Nodes;
using IntegrationHub.Application;
using IntegrationHub.Contracts;
using IntegrationHub.Domain;
using Json.Schema;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
namespace IntegrationHub.Infrastructure.Parsing;

public sealed class ComponentDefinitionSchema
{
    private readonly JsonSchema _schema;
    public string Text { get; }
    public ComponentDefinitionSchema()
    {
        var assembly = typeof(ComponentDefinitionSchema).Assembly;
        using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith("component-definition.schema.json")))!;
        using var reader = new StreamReader(stream);
        Text = reader.ReadToEnd(); _schema = JsonSchema.FromText(Text);
    }
    public ValidationResult Validate(JsonNode? node)
    {
        var result = _schema.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.List });
        var issues = new List<ValidationIssue>();
        void Visit(EvaluationResults item)
        {
            if (item.Errors is not null)
                foreach (var error in item.Errors) issues.Add(new("SCHEMA_" + error.Key.ToUpperInvariant(), ValidationSeverity.Error, error.Value, ValidationLevel.Schema, Path: item.InstanceLocation.ToString()));
            if (item.Details is not null) foreach (var child in item.Details) Visit(child);
        }
        if (!result.IsValid) Visit(result);
        return new(issues);
    }
}
public sealed class ComponentDefinitionParser(ComponentDefinitionSchema schema) : IComponentDefinitionParser, IComponentDefinitionSerializer
{
    public ComponentParseResult Parse(DefinitionRequest request)
    {
        var input = new DefinitionRequestValidator().Validate(request);
        if (!input.IsValid) return new(null, request.Format ?? "unknown", new(input.Errors.Select(e => new ValidationIssue("REQUEST", ValidationSeverity.Error, e.ErrorMessage, ValidationLevel.Schema, Path: e.PropertyName)).ToArray()));
        var format = request.Format is null ? (request.Definition.TrimStart().StartsWith('{') || request.Definition.TrimStart().StartsWith('[') ? "json" : "yaml") : request.Format == "yml" ? "yaml" : request.Format;
        try
        {
            var root = format == "json" ? JsonIntegrationDefinitionParser.ReadDocument(request.Definition) : YamlIntegrationDefinitionParser.ReadDocument(request.Definition);
            var result = schema.Validate(root);
            if (!result.IsValid) return new(null, format, result);
            var definition = root!["component"]!.Deserialize<ComponentDefinition>(CanonicalJson.Options)!;
            return new(definition, format, ComponentValidator.Validate(definition));
        }
        catch (Exception ex) when (ex is JsonException or YamlException or FormatException or ArgumentException or InvalidOperationException)
        {
            return new(null, format, new([new("SYNTAX", ValidationSeverity.Error, "Check syntax, unique property names and nesting (maximum 64 levels). YAML aliases, tags and multiple documents are not supported.", ValidationLevel.Syntax)]));
        }
    }
    public string Serialize(ComponentDefinition definition, string format)
    {
        var root = JsonSerializer.SerializeToNode(new { schemaVersion = "1.1", component = definition }, CanonicalJson.Options)!;
        var component = root["component"]!.AsObject();
        if (definition.Messages.Count == 0) component.Remove("messages");
        else
        {
            // Do not emit empty legacy sections alongside the unified source format.
            // Nonempty mixed sections are retained so validation can reject them.
            foreach (var name in new[] { "consumes", "publishes", "sends" })
                if (component[name]!.AsArray().Count == 0) component.Remove(name);
        }
        var json = root.ToJsonString(CanonicalJson.Options);
        if (format == "json") return json;
        if (format is not ("yaml" or "yml")) throw new ArgumentException("Format must be json or yaml.");
        using var doc = JsonDocument.Parse(json);
        return new SerializerBuilder().DisableAliases().WithQuotingNecessaryStrings().Build().Serialize(Convert(doc.RootElement));
    }
    private static object? Convert(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => e.EnumerateObject().ToDictionary(p => p.Name, p => Convert(p.Value)),
        JsonValueKind.Array => e.EnumerateArray().Select(Convert).ToArray(),
        JsonValueKind.String => e.GetString(), JsonValueKind.True => true, JsonValueKind.False => false,
        JsonValueKind.Number => e.GetDecimal(), _ => null
    };
}
