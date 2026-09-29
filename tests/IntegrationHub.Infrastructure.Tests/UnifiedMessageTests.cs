using System.Text.Json.Nodes;
using IntegrationHub.Domain;
using IntegrationHub.Infrastructure.Parsing;
namespace IntegrationHub.Infrastructure.Tests;

[TestFixture]
public sealed class UnifiedMessageTests
{
    private readonly ComponentDefinitionParser _parser = new(new());
    private static JsonNode Document(string action = "publishes", string type = "Event", string kind = "Topic")
    {
        var root = JsonNode.Parse("""
        {"schemaVersion":"1.1","component":{"id":"example","name":"Example","type":"Api","environment":"development",
        "messages":[{"id":"message","contract":"invoice.created","version":"1.0",
        "channel":{"namespace":"finance-dev","name":"invoices"}}]}}
        """)!;
        var message = root["component"]!["messages"]![0]!;
        message["action"] = action; message["messageType"] = type; message["channel"]!["kind"] = kind;
        return root;
    }

    [TestCase("publishes", "Event")]
    [TestCase("sends", "Command")]
    [TestCase("consumes", "Event")]
    [TestCase("consumes", "Command")]
    [TestCase("consumes", "Document")]
    [TestCase("consumes", "Request")]
    [TestCase("consumes", "Response")]
    public void Actions_round_trip_without_legacy_sections_or_duplicate_bindings(string action, string type)
    {
        var root = Document(action, type, "Queue");
        var parsed = _parser.Parse(new(root.ToJsonString()));
        parsed.Validation.IsValid.Should().BeTrue();
        parsed.Definition!.Messages.Should().ContainSingle().Which.Action.Should().Be(action);
        foreach (var format in new[] { "yaml", "json" })
        {
            var exported = _parser.Serialize(parsed.Definition, format);
            var restored = _parser.Parse(new(exported, format));
            restored.Validation.IsValid.Should().BeTrue();
            restored.Definition.Should().BeEquivalentTo(parsed.Definition);
            var json = JsonNode.Parse(_parser.Serialize(restored.Definition!, "json"))!["component"]!.AsObject();
            json.ContainsKey("messages").Should().BeTrue();
            foreach (var old in new[] { "consumes", "publishes", "sends", "consumedMessages", "publishedMessages", "sentMessages" })
                json.ContainsKey(old).Should().BeFalse();
        }
    }

    [TestCase("missing-action")]
    [TestCase("null-action")]
    [TestCase("unknown-action")]
    [TestCase("action-case")]
    [TestCase("event-command")]
    [TestCase("command-event")]
    [TestCase("http")]
    [TestCase("source-selector")]
    [TestCase("target-selector")]
    [TestCase("null-messages")]
    [TestCase("legacy-consumes")]
    [TestCase("legacy-publishes")]
    [TestCase("legacy-sends")]
    [TestCase("duplicate-id")]
    [TestCase("duplicate-route")]
    [TestCase("outgoing-subscription")]
    [TestCase("missing-subscription")]
    [TestCase("queue-subscription")]
    public void Invalid_unified_messages_cannot_be_imported(string change)
    {
        var root = Document(); var component = root["component"]!;
        var message = component["messages"]![0]!;
        switch (change)
        {
            case "missing-action": message.AsObject().Remove("action"); break;
            case "null-action": message["action"] = null; break;
            case "unknown-action": message["action"] = "produces"; break;
            case "action-case": message["action"] = "Publishes"; break;
            case "event-command": message["messageType"] = "Command"; break;
            case "command-event": message["action"] = "sends"; break;
            case "http": message["channel"]!["kind"] = "Http"; break;
            case "source-selector": message["sourceComponent"] = "other"; break;
            case "target-selector": message["targetComponent"] = "other"; break;
            case "null-messages": component["messages"] = null; break;
            case "legacy-consumes": component["consumes"] = new JsonArray(); break;
            case "legacy-publishes": component["publishes"] = new JsonArray(); break;
            case "legacy-sends": component["sends"] = new JsonArray(); break;
            case "duplicate-id": component["endpoints"] = JsonNode.Parse("[{\"id\":\"message\",\"method\":\"GET\",\"path\":\"/invoices\"}]"); break;
            case "duplicate-route": var copy = message.DeepClone(); copy["id"] = "duplicate"; component["messages"]!.AsArray().Add(copy); break;
            case "outgoing-subscription": message["subscription"] = "invalid"; break;
            case "missing-subscription": message["action"] = "consumes"; break;
            case "queue-subscription": message["action"] = "consumes"; message["channel"]!["kind"] = "Queue"; message["subscription"] = "invalid"; break;
        }
        _parser.Parse(new(root.ToJsonString())).Validation.IsValid.Should().BeFalse();
    }

    [TestCase("Topic")]
    [TestCase("Stream")]
    public void Topic_and_stream_consumers_require_their_subscription(string kind)
    {
        var root = Document("consumes", "Event", kind);
        var missing = _parser.Parse(new(root.ToJsonString()));
        missing.Validation.Issues.Should().Contain(i => i.Code == "SUBSCRIPTION_REQUIRED");
        root["component"]!["messages"]![0]!["subscription"] = "invoice-workers";
        _parser.Parse(new(root.ToJsonString())).Validation.IsValid.Should().BeTrue();
    }

    [Test]
    public void Empty_messages_are_valid_and_limits_match_the_combined_legacy_capacity()
    {
        var root = Document(); var component = root["component"]!;
        var binding = component["messages"]![0]!.DeepClone();
        component["messages"] = new JsonArray();
        var empty = _parser.Parse(new(root.ToJsonString()));
        empty.Validation.IsValid.Should().BeTrue();
        empty.Validation.Issues.Should().Contain(i => i.Code == "NO_BINDINGS");
        for (var i = 0; i < 300; i++)
        {
            var copy = binding.DeepClone(); copy["id"] = $"message-{i}"; copy["contract"] = $"invoice-{i}";
            component["messages"]!.AsArray().Add(copy);
        }
        _parser.Parse(new(root.ToJsonString())).Validation.IsValid.Should().BeTrue();
        component["messages"]!.AsArray().Add(binding);
        _parser.Parse(new(root.ToJsonString())).Validation.IsValid.Should().BeFalse();
    }
}
