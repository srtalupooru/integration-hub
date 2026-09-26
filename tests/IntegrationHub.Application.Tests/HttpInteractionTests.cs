using IntegrationHub.Contracts;
using IntegrationHub.Domain;
namespace IntegrationHub.Application.Tests;

[TestFixture]
public sealed class HttpInteractionTests
{
    private static ComponentDetail Record(ComponentDefinition d) => new(d, "", "json", 1, d.Id, DateTimeOffset.UnixEpoch, "test", false);
    private static ComponentDefinition Api() => new() { Id = "vendor-api", Name = "Vendor API", Environment = "dev", Type = NodeType.Api,
        Endpoints = [new() { Id = "create-vendor", Method = HttpVerb.POST, Path = "/vendors", Version = "1.0" }] };
    private static ComponentDefinition Caller() => new() { Id = "system", Name = "System", Environment = "dev", Type = NodeType.InternalSystem,
        Calls = [new() { Id = "create", TargetComponent = "vendor-api", Endpoint = "create-vendor", Method = HttpVerb.POST, Version = "1.0" }] };
    private static DiscoveryResult Discover(params ComponentDefinition[] components) => new ComponentDiscovery().Build(components.Select(Record).ToArray());

    [Test]
    public void Exposed_endpoint_is_not_a_message_consumer_and_does_not_require_a_publisher()
    {
        var result = Discover(Api());
        result.Integrations.Should().BeEmpty(); result.Issues.Should().BeEmpty();
        ComponentValidator.Validate(Api()).Issues.Should().BeEmpty();
    }
    [Test]
    public void Http_calls_resolve_by_endpoint_identity_and_do_not_create_broker_messages()
    {
        var result = Discover(Caller(), Api());
        var integration = result.Integrations.Single();
        var connection = integration.Connections.Should().ContainSingle().Subject;
        connection.Kind.Should().Be(ComponentInteractionKind.HttpCall);
        connection.HttpMethod.Should().Be("POST"); connection.HttpPath.Should().Be("/vendors");
        integration.Definition.Messages.Should().BeEmpty();
        integration.Definition.Edges.Single().Label.Should().Be("POST /vendors (v1.0)");
        integration.Definition.Edges.Single().Mode.Should().Be(InteractionMode.Synchronous);
        result.Issues.Should().BeEmpty();
    }
    [TestCase("target", "HTTP_TARGET_NOT_FOUND")]
    [TestCase("endpoint", "HTTP_ENDPOINT_NOT_FOUND")]
    [TestCase("environment", "HTTP_ENVIRONMENT_MISMATCH")]
    [TestCase("method", "HTTP_METHOD_MISMATCH")]
    [TestCase("version", "HTTP_VERSION_MISMATCH")]
    [TestCase("retired", "HTTP_TARGET_NOT_FOUND")]
    public void Incompatible_http_calls_never_guess_a_receiver(string difference, string code)
    {
        var api = Api(); var caller = Caller(); var call = caller.Calls[0];
        call = difference switch { "target" => call with { TargetComponent = "unknown" }, "endpoint" => call with { Endpoint = "unknown" },
            "method" => call with { Method = HttpVerb.GET }, "version" => call with { Version = "1.0.0" }, _ => call };
        caller = caller with { Calls = [call], Environment = difference == "environment" ? "prod" : "dev" };
        if (difference == "retired") api = api with { Status = IntegrationStatus.Retired };
        var result = Discover(caller, api);
        result.Integrations.Should().BeEmpty(); result.Issues.Should().ContainSingle(i => i.Code == code);
    }
    [Test]
    public void Methods_are_distinct_and_many_systems_can_call_the_same_api()
    {
        var api = Api() with { Endpoints = [Api().Endpoints[0], new() { Id = "read-vendors", Method = HttpVerb.GET, Path = "/vendors" }] };
        var second = Caller() with { Id = "second-system", Calls = [Caller().Calls[0] with { Endpoint = "read-vendors", Method = HttpVerb.GET }] };
        var result = Discover(api, Caller(), second);
        result.Integrations.Single().Connections.Should().HaveCount(2);
        result.Issues.Should().NotContain(i => i.Code == "MULTIPLE_PUBLISHERS" || i.Code == "AMBIGUOUS_HTTP_TARGET");
    }
    [Test]
    public void Api_can_send_command_and_publish_event_without_consuming_a_message()
    {
        var command = new MessageBinding { Id = "create-command", Contract = "vendors.create", Version = "1.0", MessageType = MessageType.Command,
            Channel = new() { Kind = ChannelKind.Queue, Namespace = "broker", Name = "commands" } };
        var eventBinding = command with { Id = "created-event", Contract = "vendors.created", MessageType = MessageType.Event,
            Channel = new() { Kind = ChannelKind.Topic, Namespace = "broker", Name = "events" } };
        var api = Api() with { Sends = [command], Publishes = [eventBinding] };
        var function = new ComponentDefinition { Id = "worker", Name = "Worker", Environment = "dev", Type = NodeType.AzureFunction,
            Consumes = [command with { Id = "on-command" }, eventBinding with { Id = "on-event", Subscription = "worker-events" }] };
        var integration = Discover(Caller(), api, function).Integrations.Single();
        integration.Connections.Should().HaveCount(3);
        integration.Definition.Messages.Should().HaveCount(2).And.NotContain(m => m.Type == MessageType.Request);
        integration.Definition.Edges.Should().Contain(e => e.Label.StartsWith("Command:")).And.Contain(e => e.Label.StartsWith("Event:"));
        api.Consumes.Should().BeEmpty(); ComponentValidator.Validate(api).IsValid.Should().BeTrue();
    }
    [Test]
    public void Http_cycles_are_preserved_and_reported()
    {
        var api = Api() with { Calls = [Caller().Calls[0]] };
        var result = Discover(api);
        result.Integrations.Single().HasCycle.Should().BeTrue();
        result.Integrations.Single().Definition.Messages.Should().BeEmpty();
    }
    [Test]
    public void Legacy_http_still_links_without_inventing_a_method_or_broker_message()
    {
        var binding = new MessageBinding { Id = "legacy", Contract = "vendors.create", Version = "1.0", MessageType = MessageType.Request,
            Channel = new() { Kind = ChannelKind.Http, Namespace = "api", Name = "/vendors" } };
        var caller = Caller() with { Calls = [], Publishes = [binding] };
        var api = Api() with { Endpoints = [], Consumes = [binding] };
        var result = Discover(caller, api);
        result.Integrations.Single().Connections.Single().Kind.Should().Be(ComponentInteractionKind.HttpCall);
        result.Integrations.Single().Connections.Single().HttpMethod.Should().BeNull();
        result.Integrations.Single().Definition.Messages.Should().BeEmpty();
        result.Issues.Should().Contain(i => i.Code == "LEGACY_HTTP_BINDING");
        Discover(Caller(), api).Issues.Should().Contain(i => i.Code == "HTTP_ENDPOINT_NOT_FOUND");
    }
    [Test]
    public void Duplicate_endpoints_calls_and_cross_section_ids_are_rejected()
    {
        var api = Api(); var endpoint = api.Endpoints[0];
        ComponentValidator.Validate(api with { Endpoints = [endpoint, endpoint with { Id = "duplicate" }] }).Issues.Should().Contain(i => i.Code == "DUPLICATE_ENDPOINT");
        var caller = Caller(); var call = caller.Calls[0];
        ComponentValidator.Validate(caller with { Calls = [call, call with { Id = "duplicate" }] }).Issues.Should().Contain(i => i.Code == "DUPLICATE_CALL");
        ComponentValidator.Validate(api with { Calls = [call with { Id = endpoint.Id }] }).Issues.Should().Contain(i => i.Code == "DUPLICATE_BINDING_ID");
    }
}
