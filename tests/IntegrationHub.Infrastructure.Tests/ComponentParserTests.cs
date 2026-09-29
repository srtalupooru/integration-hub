using System.Text.Json.Nodes;
using IntegrationHub.Contracts;
using IntegrationHub.Domain;
using IntegrationHub.Infrastructure.Parsing;
namespace IntegrationHub.Infrastructure.Tests;

[TestFixture]
public sealed class ComponentParserTests
{
    private readonly ComponentDefinitionParser _parser = new(new());
    [TestCase("[\"ASP.NET Core\",\"FastEndpoints\"]", 2)]
    [TestCase("[]", 0)]
    [TestCase("\"ASP.NET Core, FastEndpoints\"", 1)]
    [TestCase("\"  ASP.NET Core  \"", 1)]
    [TestCase("\"\"", 0)]
    public void Technology_lists_and_legacy_strings_export_as_lists_and_round_trip(string technology, int count)
    {
        var root = JsonNode.Parse(Json())!;
        root["component"]!["technology"] = JsonNode.Parse(technology);
        var parsed = _parser.Parse(new(root.ToJsonString()));
        parsed.Validation.IsValid.Should().BeTrue();
        parsed.Definition!.Technology.Should().HaveCount(count);
        foreach (var format in new[] { "json", "yaml" })
        {
            var exported = _parser.Serialize(parsed.Definition, format);
            var restored = _parser.Parse(new(exported, format));
            restored.Validation.IsValid.Should().BeTrue();
            restored.Definition!.Technology.Should().Equal(parsed.Definition.Technology);
        }
        JsonNode.Parse(_parser.Serialize(parsed.Definition, "json"))!["component"]!["technology"].Should().BeOfType<JsonArray>();
    }

    [TestCase("null")]
    [TestCase("{}")]
    [TestCase("42")]
    [TestCase("[42]")]
    [TestCase("[null]")]
    [TestCase("[\"\"]")]
    [TestCase("[\"   \"]")]
    [TestCase("[\" padded \"]")]
    [TestCase("[\".NET\",\".NET\"]")]
    public void Invalid_technology_lists_report_schema_errors(string technology)
    {
        var root = JsonNode.Parse(Json())!;
        root["component"]!["technology"] = JsonNode.Parse(technology);
        var parsed = _parser.Parse(new(root.ToJsonString()));
        parsed.Validation.IsValid.Should().BeFalse();
        parsed.Validation.Issues.Should().Contain(i => i.Level == ValidationLevel.Schema && i.Path!.StartsWith("/component/technology"));
    }

    [Test]
    public void Technology_defaults_and_limits_are_enforced()
    {
        var root = JsonNode.Parse(Json())!;
        root["component"]!.AsObject().Remove("technology");
        _parser.Parse(new(root.ToJsonString())).Definition!.Technology.Should().BeEmpty();
        root["component"]!["technology"] = new JsonArray(Enumerable.Range(0, 50).Select(i => JsonValue.Create($"Technology {i}")).ToArray());
        _parser.Parse(new(root.ToJsonString())).Validation.IsValid.Should().BeTrue();
        root["component"]!["technology"]!.AsArray().Add("One too many");
        _parser.Parse(new(root.ToJsonString())).Validation.IsValid.Should().BeFalse();
        root["component"]!["technology"] = new JsonArray(new string('a', 257));
        _parser.Parse(new(root.ToJsonString())).Validation.IsValid.Should().BeFalse();
    }
    private string Json()
    {
        var parsed = _parser.Parse(new(ComponentExamples.Read("vendor-api")));
        parsed.Validation.IsValid.Should().BeTrue();
        return _parser.Serialize(parsed.Definition!, "json");
    }
    [TestCase("vendor-api")]
    [TestCase("vendor-function")]
    [TestCase("elite-api")]
    [TestCase("vendor-system")]
    [TestCase("vendor-command-api")]
    [TestCase("vendor-command-function")]
    public void Shipped_examples_validate_and_round_trip_through_both_formats(string name)
    {
        var result = _parser.Parse(new(ComponentExamples.Read(name)));
        result.Validation.IsValid.Should().BeTrue();
        foreach (var format in new[] { "json", "yaml" })
        {
            var parsed = _parser.Parse(new(_parser.Serialize(result.Definition!, format), format));
            parsed.Validation.IsValid.Should().BeTrue();
            _parser.Serialize(parsed.Definition!, "json").Should().Be(_parser.Serialize(result.Definition!, "json"));
        }
    }
    [TestCase("{}")]
    [TestCase("{\"component\":null}")]
    [TestCase("{\"component\":{\"consumes\":null}}")]
    public void Missing_or_null_structures_are_schema_errors(string text)
    {
        var result = _parser.Parse(new(text)); result.Validation.IsValid.Should().BeFalse();
        result.Validation.Issues.Should().Contain(i => i.Level == ValidationLevel.Schema);
    }
    [TestCase("{\"component\":{},\"component\":{}}")]
    [TestCase("component: &c\n  id: api\nother: *c")]
    [TestCase("component: !!str test")]
    [TestCase("component: {}\n---\ncomponent: {}")]
    [TestCase("component:\n  id: one\n  id: two")]
    [TestCase("{")]
    public void Unsafe_or_ambiguous_syntax_fails_cleanly(string text)
    {
        var result = _parser.Parse(new(text)); result.Validation.IsValid.Should().BeFalse();
        result.Validation.Issues.Should().Contain(i => i.Level == ValidationLevel.Syntax);
    }
    [TestCase("unknown")]
    [TestCase("environment")]
    [TestCase("version")]
    [TestCase("null-list")]
    [TestCase("integer-enum")]
    [TestCase("whitespace-channel")]
    public void Unknown_fields_and_unsafe_identities_are_rejected(string change)
    {
        var root = JsonNode.Parse(Json())!; var c = root["component"]!;
        switch (change)
        {
            case "unknown": c["publishs"] = new JsonArray(); break;
            case "environment": c["environment"] = "Development "; break;
            case "version": c["messages"]![0]!["version"] = "*"; break;
            case "null-list": c["consumes"] = null; break;
            case "integer-enum": c["type"] = 2; break;
            case "whitespace-channel": c["messages"]![0]!["channel"]!["name"] = " "; break;
        }
        _parser.Parse(new(root.ToJsonString())).Validation.IsValid.Should().BeFalse();
    }
    [TestCase("duplicate-id", "DUPLICATE_BINDING_ID")]
    [TestCase("duplicate-route", "DUPLICATE_BINDING")]
    [TestCase("missing-subscription", "SUBSCRIPTION_REQUIRED")]
    [TestCase("wrong-direction", "BINDING_DIRECTION")]
    [TestCase("wrong-target", "TARGET_CHANNEL")]
    [TestCase("wrong-subscription", "SUBSCRIPTION_CHANNEL")]
    public void Invalid_binding_semantics_have_actionable_codes(string change, string code)
    {
        var definition = _parser.Parse(new(ComponentExamples.Read("vendor-function"))).Definition!;
        // Exercise legacy HTTP-message compatibility as well as current broker validation.
        var consume = definition.ConsumedMessages.Single() with { Action = null };
        var publish = new MessageBinding { Id = "legacy-http", Contract = "vendors.upsert", Version = "1.0", MessageType = MessageType.Request,
            Channel = new() { Kind = ChannelKind.Http, Namespace = "elite-api-dev", Name = "/vendors" }, TargetComponent = "elite-api-dev" };
        definition = definition with { Messages = [], Consumes = [consume], Publishes = [publish], Calls = [] };
        definition = change switch
        {
            "duplicate-id" => definition with { Publishes = [publish with { Id = consume.Id }] },
            "duplicate-route" => definition with { Consumes = [consume, consume with { Id = "another" }] },
            "missing-subscription" => definition with { Consumes = [consume with { Subscription = null }] },
            "wrong-direction" => definition with { Publishes = [publish with { SourceComponent = "other" }] },
            "wrong-target" => definition with { Publishes = [publish with { Channel = publish.Channel with { Kind = ChannelKind.Topic } }] },
            "wrong-subscription" => definition with { Publishes = [publish with { Subscription = "forbidden" }] }, _ => definition
        };
        var result = _parser.Parse(new(_parser.Serialize(definition, "json")));
        result.Validation.Issues.Should().Contain(i => i.Code == code && i.Severity == ValidationSeverity.Error);
    }
    [Test]
    public void Request_limits_and_unknown_formats_return_validation_errors()
    {
        _parser.Parse(new(new string('a', 1_000_001))).Validation.IsValid.Should().BeFalse();
        _parser.Parse(new(Json(), "xml")).Validation.IsValid.Should().BeFalse();
        _parser.Parse(new(Json(), ExpectedRevision: 0)).Validation.IsValid.Should().BeFalse();
    }
    [Test]
    public void Unmatched_messages_are_not_a_local_validation_error()
    {
        _parser.Parse(new(ComponentExamples.Read("vendor-function"))).Validation.IsValid.Should().BeTrue();
    }
    [TestCase("method")]
    [TestCase("path")]
    [TestCase("call-target")]
    [TestCase("null-endpoints")]
    [TestCase("command-type")]
    [TestCase("command-http")]
    public void Invalid_http_or_command_declarations_are_rejected(string change)
    {
        var root = JsonNode.Parse(_parser.Serialize(_parser.Parse(new(ComponentExamples.Read("vendor-command-api"))).Definition!, "json"))!;
        var c = root["component"]!;
        switch (change)
        {
            case "method": c["endpoints"]![0]!["method"] = "post"; break;
            case "path": c["endpoints"]![0]!["path"] = "/vendors?secret=example"; break;
            case "null-endpoints": c["endpoints"] = null; break;
            case "call-target": c["calls"] = JsonNode.Parse("[{\"id\":\"c\",\"endpoint\":\"e\",\"method\":\"POST\"}]"); break;
            case "command-type": c["messages"]![0]!["messageType"] = "Event"; break;
            case "command-http": c["messages"]![0]!["channel"]!["kind"] = "Http"; break;
        }
        _parser.Parse(new(root.ToJsonString())).Validation.IsValid.Should().BeFalse();
    }
    [Test]
    public void Legacy_http_source_and_exports_remain_readable_without_implicit_conversion()
    {
        const string source = """
        schemaVersion: "1.0"
        component:
          id: old-api
          name: Old API
          type: Api
          environment: dev
          consumes:
            - id: request
              contract: vendors.create
              version: "1.0"
              messageType: Request
              channel:
                kind: Http
                namespace: old-api
                name: /vendors
          publishes: []
        """;
        var parsed = _parser.Parse(new(source));
        parsed.Validation.IsValid.Should().BeTrue();
        parsed.Validation.Issues.Should().Contain(i => i.Code == "LEGACY_HTTP_BINDING");
        parsed.Definition!.Endpoints.Should().BeEmpty();
        _parser.Parse(new(_parser.Serialize(parsed.Definition, "yaml"))).Validation.IsValid.Should().BeTrue();
    }
}
