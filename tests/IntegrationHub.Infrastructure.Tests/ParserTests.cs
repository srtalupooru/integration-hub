using IntegrationHub.Domain;
using IntegrationHub.Infrastructure.Parsing;
using IntegrationHub.Tests;
namespace IntegrationHub.Infrastructure.Tests;

[TestFixture]
public sealed class ParserTests
{
    private readonly DefinitionSchema _schema = new();
    [Test]
    public void Json_and_yaml_generate_equivalent_canonical_models()
    {
        var json = new JsonIntegrationDefinitionParser(_schema).Parse(TestDefinitions.Json);
        var yaml = new YamlIntegrationDefinitionParser(_schema).Parse(TestDefinitions.Yaml);
        json.Validation.IsValid.Should().BeTrue(); yaml.Validation.IsValid.Should().BeTrue();
        yaml.Definition.Should().BeEquivalentTo(json.Definition);
        json.Definition!.Edges[0].Id.Should().Be("edge-1");
    }
    [TestCase("{broken", "json", ValidationLevel.Syntax)]
    [TestCase("integration: [", "yaml", ValidationLevel.Syntax)]
    [TestCase("{}", "json", ValidationLevel.Schema)]
    [TestCase("integration: {}", "yaml", ValidationLevel.Schema)]
    [TestCase("{\"integration\":null}", "json", ValidationLevel.Schema)]
    [TestCase("integration: {}\n---\nintegration: {}", "yaml", ValidationLevel.Syntax)]
    public void Invalid_definitions_have_distinct_validation_levels(string source, string format, ValidationLevel level)
    {
        var parser = format == "json" ? (IntegrationDefinitionParser)new JsonIntegrationDefinitionParser(_schema) : new YamlIntegrationDefinitionParser(_schema);
        var result = parser.Parse(source);
        result.Definition.Should().BeNull(); result.Validation.IsValid.Should().BeFalse(); result.Validation.Issues.Should().OnlyContain(i => i.Level == level);
    }
    [Test]
    public void Unknown_properties_and_invalid_enum_values_are_rejected()
    {
        var parser = new JsonIntegrationDefinitionParser(_schema);
        parser.Parse(TestDefinitions.Json.Replace("\"Draft\"", "\"NotAStatus\"")).Validation.IsValid.Should().BeFalse();
        parser.Parse(TestDefinitions.Json.Replace("\"status\"", "\"statuz\"")).Validation.IsValid.Should().BeFalse();
    }
    [Test]
    public void Duplicate_json_properties_are_rejected() => new JsonIntegrationDefinitionParser(_schema).Parse("{\"integration\":{},\"integration\":{}}").Validation.Issues.Should().Contain(i => i.Level == ValidationLevel.Syntax);
    [TestCase("integration: &a {child: *a}")]
    [TestCase("integration: !!str hello")]
    [TestCase("integration: {id: a, id: b}")]
    public void Unsafe_yaml_constructs_are_rejected(string source) => new YamlIntegrationDefinitionParser(_schema).Parse(source).Validation.Issues.Should().Contain(i => i.Level == ValidationLevel.Syntax);
    [Test]
    public void Deep_nesting_is_rejected_without_recursion_failure() => new JsonIntegrationDefinitionParser(_schema).Parse(new string('[', 100) + new string(']', 100)).Validation.IsValid.Should().BeFalse();
    [TestCase("json")]
    [TestCase("yaml")]
    public void Exported_definition_round_trips_through_the_published_schema(string format)
    {
        var original = new JsonIntegrationDefinitionParser(_schema).Parse(TestDefinitions.Json).Definition!;
        var output = new DefinitionSerializer().Serialize(original, format);
        var result = format == "json" ? new JsonIntegrationDefinitionParser(_schema).Parse(output) : new YamlIntegrationDefinitionParser(_schema).Parse(output);
        result.Validation.Issues.Should().BeEmpty(); result.Definition.Should().BeEquivalentTo(original);
    }
}
