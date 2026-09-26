using System.Text.Json;
using System.Text.Json.Nodes;
using IntegrationHub.Application;
using IntegrationHub.Contracts;
using IntegrationHub.Domain;
using Json.Schema;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;
namespace IntegrationHub.Infrastructure.Parsing;

public sealed class DefinitionSchema
{
    private readonly JsonSchema _schema;
    public string Text { get; }
    public DefinitionSchema()
    {
        var assembly = typeof(DefinitionSchema).Assembly;
        using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith("integration-definition.schema.json")))!;
        using var reader = new StreamReader(stream);
        Text = reader.ReadToEnd();
        _schema = JsonSchema.FromText(Text);
    }
    public IReadOnlyList<ValidationIssue> Validate(JsonNode? node)
    {
        var result = _schema.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid) return [];
        var issues = new List<ValidationIssue>();
        void Visit(EvaluationResults item)
        {
            if (item.Errors is not null)
                foreach (var error in item.Errors) issues.Add(new("SCHEMA_" + error.Key.ToUpperInvariant(), ValidationSeverity.Error, error.Value, ValidationLevel.Schema, Path: item.InstanceLocation.ToString(), SuggestedResolution: "Correct this property using the published definition schema."));
            if (item.Details is not null) foreach (var child in item.Details) Visit(child);
        }
        Visit(result);
        return issues;
    }
}
public abstract class IntegrationDefinitionParser(DefinitionSchema schema) : IIntegrationDefinitionParser
{
    public abstract string Format { get; }
    protected abstract JsonNode? Read(string source);
    public ParseResult Parse(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > 1_000_000 || System.Text.Encoding.UTF8.GetByteCount(source) > 1_000_000)
            return Failure("SIZE", "Definition must be nonempty and at most 1 MB in UTF-8.", ValidationLevel.Syntax);
        JsonNode? root;
        try { root = Read(source); }
        catch (Exception ex) when (ex is JsonException or YamlException or FormatException or ArgumentException or InvalidOperationException)
        { return Failure("SYNTAX", "Invalid definition syntax: " + SafeMessage(ex), ValidationLevel.Syntax); }
        var issues = schema.Validate(root);
        if (issues.Count > 0) return new(null, Format, new(issues));
        try
        {
            var definition = root!["integration"]!.Deserialize<IntegrationDefinition>(CanonicalJson.Options)!;
            // Stable positional IDs are generated only when authors omit explicit IDs.
            definition = definition with { Edges = definition.Edges.Select((e, i) => string.IsNullOrEmpty(e.Id) ? e with { Id = $"edge-{i + 1}" } : e).ToArray() };
            return new(definition, Format, ValidationResult.Success);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        { return Failure("SCHEMA", "Definition could not be converted to the canonical model.", ValidationLevel.Schema); }
    }
    private ParseResult Failure(string code, string message, ValidationLevel level) => new(null, Format, new([new(code, ValidationSeverity.Error, message, level)]));
    private static string SafeMessage(Exception ex) => ex is JsonException json ? $"JSON error at line {json.LineNumber}, byte {json.BytePositionInLine}." : "Check indentation, unique property names, scalar values, and nesting. YAML aliases and explicit tags are not supported.";
}
public sealed class JsonIntegrationDefinitionParser(DefinitionSchema schema) : IntegrationDefinitionParser(schema)
{
    public override string Format => "json";
    protected override JsonNode? Read(string source) => ReadDocument(source);
    internal static JsonNode? ReadDocument(string source)
    {
        using var document = JsonDocument.Parse(source, new JsonDocumentOptions { MaxDepth = 64 });
        void Check(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in element.EnumerateObject())
                {
                    if (!names.Add(p.Name)) throw new FormatException("Duplicate JSON property.");
                    Check(p.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) Check(child);
        }
        Check(document.RootElement);
        return JsonNode.Parse(source, documentOptions: new JsonDocumentOptions { MaxDepth = 64 });
    }
}
public sealed class YamlIntegrationDefinitionParser(DefinitionSchema schema) : IntegrationDefinitionParser(schema)
{
    public override string Format => "yaml";
    protected override JsonNode? Read(string source) => ReadDocument(source);
    internal static JsonNode? ReadDocument(string source)
    {
        var parser = new Parser(new StringReader(source));
        var depth = 0;
        while (parser.MoveNext())
        {
            if (parser.Current is AnchorAlias) throw new FormatException("Aliases are not supported.");
            if (parser.Current is NodeEvent node && !node.Tag.IsEmpty) throw new FormatException("Explicit tags are not supported.");
            if (parser.Current is MappingStart or SequenceStart && ++depth > 64) throw new FormatException("Nesting limit exceeded.");
            if (parser.Current is MappingEnd or SequenceEnd) depth--;
        }
        var yaml = new YamlStream(); yaml.Load(new StringReader(source));
        if (yaml.Documents.Count != 1) throw new FormatException("Exactly one document is required.");
        return Convert(yaml.Documents[0].RootNode);
    }
    private static JsonNode? Convert(YamlNode node, string? property = null)
    {
        if (node is YamlMappingNode mapping)
        {
            var result = new JsonObject();
            foreach (var pair in mapping.Children)
            {
                if (pair.Key is not YamlScalarNode key || key.Value is null || result.ContainsKey(key.Value)) throw new FormatException("Invalid or duplicate mapping key.");
                result[key.Value] = Convert(pair.Value, key.Value);
            }
            return result;
        }
        if (node is YamlSequenceNode sequence) return new JsonArray(sequence.Children.Select(n => Convert(n)).ToArray());
        if (node is not YamlScalarNode scalar) throw new FormatException("Unsupported YAML node.");
        var value = scalar.Value ?? "";
        if (scalar.Style == ScalarStyle.Plain && property != "version")
        {
            if (value is "null" or "Null" or "NULL" or "~" or "") return null;
            if (bool.TryParse(value, out var boolean)) return JsonValue.Create(boolean);
        }
        return JsonValue.Create(value);
    }
}
