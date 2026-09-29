using System.Text.Json.Nodes;
using IntegrationHub.Application;
using IntegrationHub.Domain;
using IntegrationHub.Infrastructure.Parsing;
namespace IntegrationHub.Infrastructure.Tests;

[TestFixture]
public sealed class ComponentProcessingTests
{
    private readonly ComponentDefinitionParser _parser = new(new());
    private JsonNode Document() => JsonNode.Parse(_parser.Serialize(_parser.Parse(new(ComponentExamples.Read("saga-worker"))).Definition!, "json"))!;

    [TestCase("saga-api")]
    [TestCase("saga-worker")]
    [TestCase("saga-payment-worker")]
    public void Samples_validate_and_round_trip_in_both_formats(string name)
    {
        var parsed = _parser.Parse(new(ComponentExamples.Read(name)));
        parsed.Validation.IsValid.Should().BeTrue(string.Join("; ", parsed.Validation.Issues.Select(i => i.Message)));
        foreach (var format in new[] { "json", "yaml" })
        {
            var restored = _parser.Parse(new(_parser.Serialize(parsed.Definition!, format), format));
            restored.Validation.IsValid.Should().BeTrue(string.Join("; ", restored.Validation.Issues.Select(i => i.Message)));
            restored.Definition.Should().BeEquivalentTo(parsed.Definition);
        }
    }

    [TestCase("framework")]
    [TestCase("null")]
    [TestCase("unknown-field")]
    [TestCase("duplicate-processor")]
    [TestCase("duplicate-handles")]
    [TestCase("input")]
    [TestCase("output")]
    [TestCase("call")]
    [TestCase("timeout-reference")]
    [TestCase("send-only")]
    [TestCase("missing-correlation")]
    [TestCase("missing-property")]
    [TestCase("internal-id")]
    [TestCase("header")]
    [TestCase("wrong-mode-field")]
    [TestCase("custom")]
    [TestCase("saga-id-start")]
    [TestCase("not-found")]
    [TestCase("zero-delay")]
    [TestCase("negative-delay")]
    [TestCase("bad-delay")]
    [TestCase("two-schedules")]
    [TestCase("duplicate-timeout")]
    public void Invalid_processing_is_rejected_before_save(string change)
    {
        var root = Document(); var p = root["component"]!["processing"]!;
        var saga = p["sagas"]![0]!; var rule = saga["handles"]![0]!;
        var timeout = saga["timeouts"]![0]!;
        switch (change)
        {
            case "framework": p["framework"] = "unknown"; break;
            case "null": root["component"]!["processing"] = null; break;
            case "unknown-field": rule["unexpected"] = true; break;
            case "duplicate-processor": saga["id"] = "audit-invoice"; break;
            case "duplicate-handles": saga["handles"]!.AsArray().Add(rule.DeepClone()); break;
            case "input": rule["message"] = "request-payment"; break;
            case "output": rule["outputs"] = new JsonArray("on-invoice-created"); break;
            case "call": rule["calls"] = new JsonArray("absent"); break;
            case "timeout-reference": rule["requestsTimeouts"] = new JsonArray("other-saga-timeout"); break;
            case "send-only": p["sendOnly"] = true; break;
            case "missing-correlation": rule.AsObject().Remove("correlation"); break;
            case "missing-property": saga.AsObject().Remove("correlationProperty"); break;
            case "internal-id": saga["correlationProperty"] = "Id"; break;
            case "header": rule["correlation"] = JsonNode.Parse("{\"mode\":\"Header\"}"); break;
            case "wrong-mode-field": rule["correlation"]!["header"] = "InvoiceId"; break;
            case "custom": rule["correlation"] = JsonNode.Parse("{\"mode\":\"Custom\"}"); break;
            case "saga-id-start": rule["correlation"] = JsonNode.Parse("{\"mode\":\"SagaId\"}"); break;
            case "not-found": saga["whenNotFound"] = "Custom"; saga["notFoundDescription"] = ""; break;
            case "zero-delay": timeout["delay"] = "PT0S"; break;
            case "negative-delay": timeout["delay"] = "-PT1S"; break;
            case "bad-delay": timeout["delay"] = "tomorrow"; break;
            case "two-schedules": timeout["schedule"] = "From due date"; break;
            case "duplicate-timeout": saga["timeouts"]!.AsArray().Add(timeout.DeepClone()); break;
        }
        _parser.Parse(new(root.ToJsonString())).Validation.IsValid.Should().BeFalse(change);
    }

    [TestCase("Header", "{\"mode\":\"Header\",\"header\":\"invoice-id\"}")]
    [TestCase("Custom", "{\"mode\":\"Custom\",\"description\":\"Finder uses tenant and invoice ID.\"}")]
    [TestCase("SagaId", "{\"mode\":\"SagaId\"}")]
    public void Alternative_correlation_modes_can_document_continuations(string mode, string correlation)
    {
        var root = Document(); var rule = root["component"]!["processing"]!["sagas"]![0]!["handles"]![1]!;
        rule["correlation"] = JsonNode.Parse(correlation);
        var parsed = _parser.Parse(new(root.ToJsonString()));
        parsed.Validation.IsValid.Should().BeTrue();
        parsed.Definition!.Processing!.Sagas[0].Handles[1].Correlation!.Mode.Should().Be(mode);
    }

    [Test]
    public void Multiple_starters_and_multiple_sagas_share_delivery_without_becoming_competing_consumers()
    {
        var root = Document(); var p = root["component"]!["processing"]!;
        p["sagas"]![0]!["handles"]![1]!["startsSaga"] = true;
        var copy = p["sagas"]![0]!.DeepClone(); copy["id"] = "another-saga";
        p["sagas"]!.AsArray().Add(copy);
        var parsed = _parser.Parse(new(root.ToJsonString()));
        parsed.Validation.IsValid.Should().BeTrue();
        parsed.Validation.Issues.Should().Contain(i => i.Code == "PROCESSING_MULTIPLE_HANDLERS")
            .And.Contain(i => i.Code == "SAGA_SHARED_DATA");
    }

    [Test]
    public void Partial_sagas_and_recurring_or_unreachable_timeouts_report_warnings_without_hanging()
    {
        var root = Document(); var p = root["component"]!["processing"]!;
        p["outbox"] = false; var saga = p["sagas"]![0]!;
        saga["handles"]![0]!["startsSaga"] = false;
        saga["handles"]![0]!["requestsTimeouts"] = new JsonArray();
        saga["timeouts"]![0]!["requestsTimeouts"] = new JsonArray("payment-deadline");
        var parsed = _parser.Parse(new(root.ToJsonString()));
        parsed.Validation.IsValid.Should().BeTrue();
        var codes = parsed.Validation.Issues.Select(i => i.Code);
        codes.Should().Contain(new[] { "SAGA_NO_START", "SAGA_TIMEOUT_CYCLE", "SAGA_TIMEOUT_UNREACHABLE", "SAGA_COMPLETION_TIMEOUT", "SAGA_COMPLETION_CONSISTENCY" });
    }

    [Test]
    public void Rule_limit_prevents_unbounded_processing_work()
    {
        var component = _parser.Parse(new(ComponentExamples.Read("saga-worker"))).Definition!;
        var p = component.Processing!;
        var handler = p.Handlers[0] with { Handles = Enumerable.Range(0, 501).Select(_ => new HandlingRule { Message = "on-invoice-created" }).ToArray() };
        ComponentProcessingValidator.Validate(component with { Processing = p with { Handlers = [handler] } })
            .Should().Contain(i => i.Code == "PROCESSING_LIMIT" && i.Severity == ValidationSeverity.Error);
    }
}
