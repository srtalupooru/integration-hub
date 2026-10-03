using System.Text.Json.Nodes;
using IntegrationHub.Domain;
using IntegrationHub.Infrastructure.Parsing;
using IntegrationHub.Web;
using IntegrationHub.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace IntegrationHub.Api.Tests;

[TestFixture]
public sealed class ComponentFormTests
{
    private readonly ComponentDefinitionParser _parser = new(new());
    private static JsonObject Schema() => JsonNode.Parse(new ComponentDefinitionSchema().Text)!.AsObject();

    [Test]
    public void Form_builds_a_valid_function_with_consumed_and_published_messages()
    {
        var root = Schema(); var document = ComponentFormSchema.NewDocument(); var component = document["component"]!.AsObject();
        component["id"] = "invoice-worker"; component["name"] = "Invoice worker"; component["type"] = "AzureFunction";
        var messageSchema = root["$defs"]!["message"]!.AsObject();
        var input = ComponentFormSchema.Create(root, messageSchema).AsObject();
        input["id"] = "on-created"; input["contract"] = "InvoiceCreated";
        ComponentFormSchema.Set(input, "action", "consumes", new() { ["type"] = "string" }, true);
        input["channel"]!["namespace"] = "finance-dev"; input["channel"]!["name"] = "invoices"; input["subscription"] = "worker";
        var output = input.DeepClone().AsObject(); output["id"] = "completed"; output["contract"] = "InvoiceCompleted";
        ComponentFormSchema.Set(output, "action", "publishes", new() { ["type"] = "string" }, true);
        // Existing values stay visible until explicitly removed, rather than losing route information.
        output["subscription"]!.GetValue<string>().Should().Be("worker");
        ComponentFormSchema.Visible(output, "subscription").Should().BeTrue();
        ComponentFormSchema.Set(output, "subscription", "", new() { ["type"] = "string" }, false);
        component["messages"] = new JsonArray(input, output);
        component["technology"] = new JsonArray("Azure Functions", ".NET", "NServiceBus");
        var parsed = _parser.Parse(new(ComponentFormSchema.Serialize(document)));
        parsed.Validation.IsValid.Should().BeTrue(string.Join(";", parsed.Validation.Issues.Select(i => i.Message)));
        parsed.Definition!.ConsumedMessages.Should().ContainSingle(); parsed.Definition.PublishedMessages.Should().ContainSingle();
        parsed.Definition.Technology.Should().HaveCount(3);
    }

    [TestCase("vendor-api")]
    [TestCase("vendor-function")]
    [TestCase("vendor-system")]
    [TestCase("saga-api")]
    [TestCase("saga-worker")]
    [TestCase("saga-payment-worker")]
    public void Editing_metadata_preserves_all_interactions_and_processing(string sample)
    {
        var original = _parser.Parse(new(ComponentExamples.Read(sample))).Definition!;
        var document = JsonNode.Parse(_parser.Serialize(original, "json"))!.AsObject();
        document["component"]!["owner"] = "Updated team";
        var parsed = _parser.Parse(new(ComponentFormSchema.Serialize(document)));
        parsed.Validation.IsValid.Should().BeTrue();
        parsed.Definition.Should().BeEquivalentTo(original with { Owner = "Updated team" });
    }

    [Test]
    public void Legacy_bindings_survive_a_form_edit_without_silently_dropping_conflicting_sections()
    {
        var source = ComponentExamples.Read("vendor-api").Replace("  messages:", "  publishes:").Replace("      action: publishes\n", "");
        var original = _parser.Parse(new(source)).Definition!;
        var document = JsonNode.Parse(_parser.Serialize(original, "json"))!.AsObject();
        var component = document["component"]!.AsObject();
        ComponentFormSchema.HasLegacy(component).Should().BeTrue();
        component["name"] = "Updated API";
        var parsed = _parser.Parse(new(ComponentFormSchema.Serialize(document)));
        parsed.Validation.IsValid.Should().BeTrue();
        parsed.Definition.Should().BeEquivalentTo(original with { Name = "Updated API" });
        component["messages"] = new JsonArray(JsonNode.Parse("{\"id\":\"do-not-drop\"}"));
        var conflicting = JsonNode.Parse(ComponentFormSchema.Serialize(document))!["component"]!;
        conflicting["messages"]!.AsArray().Should().ContainSingle();
        conflicting["publishes"]!.AsArray().Should().ContainSingle();
    }

    [Test]
    public void Source_fields_have_safe_defaults_and_conditionally_required_fields_are_available()
    {
        var root = Schema(); var properties = root["properties"]!["component"]!["properties"]!.AsObject();
        ComponentFormSchema.Create(root, properties["processing"]!.AsObject())["framework"]!.GetValue<string>().Should().Be("NServiceBus");
        ComponentFormSchema.Resolve(root, properties["technology"]!.AsObject())["type"]!.GetValue<string>().Should().Be("array");
        var binding = ComponentFormSchema.Create(root, root["$defs"]!["message"]!.AsObject()).AsObject();
        ComponentFormSchema.Visible(binding, "subscription").Should().BeFalse();
        ComponentFormSchema.Set(binding, "action", "sends", new() { ["type"] = "string" }, true);
        binding["messageType"]!.GetValue<string>().Should().Be("Command");
        ComponentFormSchema.Set(binding, "action", "consumes", new() { ["type"] = "string" }, true);
        ComponentFormSchema.Visible(binding, "subscription").Should().BeTrue();
        ComponentFormSchema.Required(new(), binding, "subscription", "/component/messages/0").Should().BeTrue();
        binding["channel"]!["kind"] = "Queue";
        ComponentFormSchema.Visible(binding, "subscription").Should().BeFalse();
        ComponentFormSchema.Required(new(), binding, "subscription", "/component/messages/0").Should().BeFalse();
        var correlation = new JsonObject { ["mode"] = "Header" };
        ComponentFormSchema.Visible(correlation, "header").Should().BeTrue();
        ComponentFormSchema.Visible(correlation, "messageProperty").Should().BeFalse();
        ComponentFormSchema.Required(new(), correlation, "header", "").Should().BeTrue();
        ComponentFormSchema.Required(new(), correlation, "messageProperty", "").Should().BeFalse();
    }

    [TestCase("vendor-api")]
    [TestCase("saga-worker")]
    public async Task Full_form_renders_named_fields_and_preserves_advanced_values(string sample)
    {
        var doc = JsonNode.Parse(_parser.Serialize(_parser.Parse(new(ComponentExamples.Read(sample))).Definition!, "json"))!.AsObject();
        doc["component"]!["owner"] = "<script>unsafe</script>";
        await using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<ComponentDefinitionForm>(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(ComponentDefinitionForm.Schema)] = Schema(), [nameof(ComponentDefinitionForm.Document)] = doc, [nameof(ComponentDefinitionForm.LockId)] = true
        }))).ToHtmlString());
        html.Should().Contain("field-component-name").And.Contain("field-component-id").And.Contain("Component type")
            .And.Contain("Technologies").And.Contain("&lt;script&gt;").And.NotContain("<script>").And.NotContain("field.Key");
        if (sample == "saga-worker") html.Should().Contain("InvoiceId").And.Contain("PT30M").And.Contain("payment-deadline").And.Contain("Finance.InvoiceProcess");
        else html.Should().Contain("/vendors").And.Contain("VendorCreated");
    }
}
