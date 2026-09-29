using IntegrationHub.Contracts;
using IntegrationHub.Domain;
namespace IntegrationHub.Application.Tests;

[TestFixture]
public sealed class ComponentDiscoveryTests
{
    private readonly ComponentDiscovery _discovery = new();
    private static readonly string SchemaHash = new('a', 64);
    private static MessageBinding Binding(string id = "event", string contract = "vendor.created", ChannelKind kind = ChannelKind.Topic, bool consume = false) => new()
    {
        Id = id, Contract = contract, Version = "1.0", MessageType = MessageType.Event,
        Channel = new() { Kind = kind, Namespace = "broker-dev", Name = "vendors" },
        Subscription = consume && kind is ChannelKind.Topic or ChannelKind.Stream ? "sync" : null, SchemaFingerprint = SchemaHash
    };
    private static ComponentDetail Component(string id, MessageBinding[]? publishes = null, MessageBinding[]? consumes = null, string environment = "development") =>
        new(new() { Id = id, Name = id, Type = NodeType.Api, Environment = environment, Publishes = publishes ?? [], Consumes = consumes ?? [] }, "", "json", 1, id + "-hash", DateTimeOffset.UnixEpoch, "test", false);
    private static ComponentDetail Producer(string id = "api") => Component(id, [Binding()]);
    private static ComponentDetail Consumer(string id = "function") => Component(id, consumes: [Binding(consume: true)]);

    [Test]
    public void Processing_evidence_is_deterministic_and_does_not_invent_routes_or_causality()
    {
        var producer = Producer() with { Definition = Producer().Definition with { Processing = new()
        {
            Endpoint = "Publisher", Sagas = [new() { Id = "expiry", Timeouts = [new()
            { Id = "deadline", StateType = "TimeoutState", Delay = "PT1H", Outputs = ["event"], CompletesSaga = true }] }]
        } } };
        var consumer = Consumer() with { Definition = Consumer().Definition with { Processing = new()
        {
            Endpoint = "Consumer", Handlers = [new() { Id = "audit", Handles = [new() { Message = "event" }] },
                new() { Id = "process", Handles = [new() { Message = "event", Condition = "Only when ready" }] }]
        } } };
        var result = _discovery.Build([producer, consumer]);
        _discovery.Build([consumer, producer]).Should().BeEquivalentTo(result, o => o.WithStrictOrdering());
        var flow = result.Integrations.Single(); flow.Definition.Nodes.Should().HaveCount(2);
        var link = flow.Connections.Should().ContainSingle().Subject;
        link.HandledBy.Select(p => p.ProcessorId).Should().Equal("audit", "process");
        link.ProducedBy.Should().ContainSingle(r => r.Kind == "Saga timeout" && r.Trigger == "deadline" && r.CompletesSaga);
        flow.Processing.Should().HaveCount(2);
        var mismatch = consumer with { Definition = consumer.Definition with { Environment = "production" } };
        _discovery.Build([producer, mismatch]).Integrations.Should().BeEmpty();
        _discovery.Build([producer, consumer with { IsArchived = true }]).Integrations.Should().BeEmpty();
        var noEvidence = consumer with { Definition = consumer.Definition with { Processing = null } };
        var remaining = _discovery.Build([producer, noEvidence]).Integrations.Single();
        remaining.Id.Should().Be(flow.Id); remaining.Connections.Single().HandledBy.Should().BeEmpty();
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void Unified_and_legacy_components_resolve_to_the_same_connections(bool newProducer, bool newConsumer)
    {
        var producer = Producer(); var consumer = Consumer();
        var expected = _discovery.Build([producer, consumer]);
        if (newProducer) producer = producer with { Definition = producer.Definition with { Publishes = [], Messages = [Binding() with { Action = "publishes" }] } };
        if (newConsumer) consumer = consumer with { Definition = consumer.Definition with { Consumes = [], Messages = [Binding(consume: true) with { Action = "consumes" }] } };
        _discovery.Build([consumer, producer]).Should().BeEquivalentTo(expected);
        var changed = consumer.Definition.ConsumedMessages.Single() with { Version = "2.0" };
        consumer = consumer with { Definition = newConsumer ? consumer.Definition with { Messages = [changed] } : consumer.Definition with { Consumes = [changed] } };
        _discovery.Build([producer, consumer]).Integrations.Should().BeEmpty();
    }

    [Test]
    public void Api_function_receiver_chain_generates_one_integration_with_evidence()
    {
        var function = Component("function", [Binding("out", "vendor.synced")], [Binding("in", consume: true)]);
        var receiver = Component("receiver", consumes: [Binding(contract: "vendor.synced", consume: true)]);
        var result = _discovery.Build([Producer(), function, receiver]);
        var integration = result.Integrations.Should().ContainSingle().Subject;
        integration.Connections.Should().HaveCount(2);
        integration.Sources.Should().HaveCount(3);
        integration.Definition.Messages.Should().HaveCount(2);
        integration.Connections.Should().Contain(c => c.ProducerId == "api" && c.ConsumerId == "function" && c.ConsumeBindingId == "in");
        result.Issues.Should().BeEmpty();
        result.UnlinkedComponentIds.Should().BeEmpty();
    }
    [TestCase("environment", "ENVIRONMENT_MISMATCH")]
    [TestCase("version", "VERSION_MISMATCH")]
    [TestCase("namespace", "CHANNEL_MISMATCH")]
    [TestCase("channel", "CHANNEL_MISMATCH")]
    [TestCase("kind", "CHANNEL_MISMATCH")]
    [TestCase("type", "MESSAGE_TYPE_MISMATCH")]
    [TestCase("content", "PAYLOAD_MISMATCH")]
    [TestCase("schema", "PAYLOAD_MISMATCH")]
    [TestCase("contract", "UNRESOLVED_CONSUMER")]
    public void Incompatible_declarations_do_not_link(string difference, string code)
    {
        var c = Consumer(); var b = c.Definition.Consumes[0];
        b = difference switch
        {
            "version" => b with { Version = "1.0.0" },
            "namespace" => b with { Channel = b.Channel with { Namespace = "broker-prod" } },
            "channel" => b with { Channel = b.Channel with { Name = "Vendors" } },
            "kind" => b with { Channel = b.Channel with { Kind = ChannelKind.Queue }, Subscription = null },
            "type" => b with { MessageType = MessageType.Command },
            "content" => b with { ContentType = "application/xml" },
            "schema" => b with { SchemaFingerprint = new string('b', 64) },
            "contract" => b with { Contract = "vendor.updated" }, _ => b
        };
        c = c with { Definition = c.Definition with { Environment = difference == "environment" ? "production" : "development", Consumes = [b] } };
        var result = _discovery.Build([Producer(), c]);
        result.Integrations.Should().BeEmpty();
        result.UnlinkedComponentIds.Should().HaveCount(2);
        result.Issues.Should().Contain(i => i.Code == code && i.ComponentId == "function");
    }
    [Test]
    public void Fan_out_to_independent_subscriptions_is_preserved()
    {
        var second = Consumer("audit"); second = second with { Definition = second.Definition with { Consumes = [Binding(consume: true) with { Subscription = "audit" }] } };
        var result = _discovery.Build([Producer(), Consumer(), second]);
        result.Integrations.Single().Connections.Should().HaveCount(2);
        result.Issues.Should().NotContain(i => i.Code == "COMPETING_CONSUMERS");
    }
    [TestCase(ChannelKind.Queue)]
    [TestCase(ChannelKind.Topic)]
    [TestCase(ChannelKind.Stream)]
    public void Shared_queues_subscriptions_and_consumer_groups_are_explicitly_competing(ChannelKind kind)
    {
        var result = _discovery.Build([Component("api", [Binding(kind: kind)]), Component("worker-a", consumes: [Binding(kind: kind, consume: true)]), Component("worker-b", consumes: [Binding(kind: kind, consume: true)])]);
        result.Integrations.Single().Connections.Should().HaveCount(2);
        result.Issues.Count(i => i.Code == "COMPETING_CONSUMERS").Should().Be(2);
    }
    [Test]
    public void Multiple_publishers_link_and_explicit_source_restricts_them()
    {
        var consumer = Consumer();
        var result = _discovery.Build([Producer(), Producer("other-api"), consumer]);
        result.Integrations.Single().Connections.Should().HaveCount(2);
        result.Issues.Should().Contain(i => i.Code == "MULTIPLE_PUBLISHERS");
        consumer = consumer with { Definition = consumer.Definition with { Consumes = [Binding(consume: true) with { SourceComponent = "api" }] } };
        result = _discovery.Build([Producer(), Producer("other-api"), consumer]);
        result.Integrations.Single().Connections.Should().ContainSingle().Which.ProducerId.Should().Be("api");
        result.UnlinkedComponentIds.Should().Contain("other-api");
    }
    [Test]
    public void Unknown_pinned_publisher_is_not_replaced_with_another_publisher()
    {
        var consumer = Component("function", consumes: [Binding(consume: true) with { SourceComponent = "missing" }]);
        var result = _discovery.Build([Producer(), consumer]);
        result.Integrations.Should().BeEmpty();
        result.Issues.Should().Contain(i => i.Code == "SOURCE_NOT_FOUND");
    }
    [Test]
    public void Ambiguous_http_destinations_are_blocked_until_a_target_is_declared()
    {
        var producer = Component("caller", [Binding(kind: ChannelKind.Http)]);
        var first = Component("api-a", consumes: [Binding(kind: ChannelKind.Http)]);
        var second = Component("api-b", consumes: [Binding(kind: ChannelKind.Http)]);
        var result = _discovery.Build([producer, first, second]);
        result.Integrations.Should().BeEmpty();
        result.Issues.Should().Contain(i => i.Code == "AMBIGUOUS_HTTP_TARGET");
        producer = producer with { Definition = producer.Definition with { Publishes = [Binding(kind: ChannelKind.Http) with { TargetComponent = "api-b" }] } };
        result = _discovery.Build([producer, first, second]);
        result.Integrations.Single().Connections.Should().ContainSingle().Which.ConsumerId.Should().Be("api-b");
        result.Integrations.Single().Definition.Edges.Single().Mode.Should().Be(InteractionMode.Synchronous);
    }
    [Test]
    public void Missing_fingerprint_is_reported_without_claiming_schema_compatibility()
    {
        var producer = Component("api", [Binding() with { SchemaFingerprint = null }]);
        var result = _discovery.Build([producer, Consumer()]);
        result.Integrations.Should().ContainSingle();
        result.Issues.Should().Contain(i => i.Code == "SCHEMA_UNVERIFIED");
    }
    [Test]
    public void Cycles_and_self_delivery_terminate_and_are_reported()
    {
        var self = Component("self", [Binding("out")], [Binding("in", consume: true)]);
        var result = _discovery.Build([self]);
        result.Integrations.Single().HasCycle.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "CYCLIC_FLOW");
        var a = Component("a", [Binding("out", "first")], [Binding("in", "second", consume: true)]);
        var b = Component("b", [Binding("out", "second")], [Binding("in", "first", consume: true)]);
        result = _discovery.Build([a, b]);
        result.Integrations.Single().Connections.Should().HaveCount(2);
        result.Integrations.Single().HasCycle.Should().BeTrue();
    }
    [Test]
    public void Reordering_input_is_deterministic_and_revision_edits_keep_network_identity()
    {
        var a = Producer(); var b = Consumer();
        var first = _discovery.Build([a, b]); var reverse = _discovery.Build([b, a]);
        System.Text.Json.JsonSerializer.Serialize(first).Should().Be(System.Text.Json.JsonSerializer.Serialize(reverse));
        var edited = _discovery.Build([a with { Revision = 2, DefinitionHash = "changed" }, b]);
        edited.Fingerprint.Should().NotBe(first.Fingerprint);
        edited.Integrations.Single().Id.Should().Be(first.Integrations.Single().Id);
        edited.Integrations.Single().Sources.Single(s => s.Id == "api").Revision.Should().Be(2);
    }
    [Test]
    public void Archive_and_retirement_remove_connections_without_destroying_unmatched_components()
    {
        var result = _discovery.Build([Producer() with { IsArchived = true }, Consumer()]);
        result.Integrations.Should().BeEmpty(); result.UnlinkedComponentIds.Should().Equal("function");
        var retired = Producer(); retired = retired with { Definition = retired.Definition with { Status = IntegrationStatus.Retired } };
        result = _discovery.Build([retired, Consumer()]);
        result.RetiredComponentIds.Should().Equal("api"); result.Integrations.Should().BeEmpty();
    }
    [Test]
    public void Disconnected_flows_are_separate_and_bridging_them_changes_membership_identity()
    {
        var a = Producer(); var b = Consumer();
        var c = Component("c", [Binding(contract: "other")]); var d = Component("d", consumes: [Binding(contract: "other", consume: true)]);
        var separate = _discovery.Build([a, b, c, d]);
        separate.Integrations.Should().HaveCount(2);
        b = b with { Definition = b.Definition with { Publishes = [Binding("bridge", "other")] } };
        var merged = _discovery.Build([a, b, c, d]);
        merged.Integrations.Should().ContainSingle();
        separate.Integrations.Select(i => i.Id).Should().NotContain(merged.Integrations.Single().Id);
    }
    [Test]
    public void No_components_or_no_bindings_do_not_create_fictitious_integrations()
    {
        _discovery.Build([]).Integrations.Should().BeEmpty();
        _discovery.Build([Component("empty")]).UnlinkedComponentIds.Should().Equal("empty");
    }
    [Test]
    public void Multiple_versions_and_channels_do_not_cross_link()
    {
        var p = Component("p", [Binding("v1"), Binding("v2") with { Version = "2.0" }]);
        var c = Component("c", consumes: [Binding("in-v1", consume: true), Binding("in-v2", consume: true) with { Version = "2.0" }]);
        var result = _discovery.Build([p, c]);
        result.Integrations.Single().Connections.Should().HaveCount(2);
        result.Integrations.Single().Definition.Messages.Select(m => m.Name).Distinct().Should().HaveCount(2);
    }
    [Test]
    public void Cancellation_is_observed_and_large_catalogues_fail_explicitly()
    {
        var cancelled = () => _discovery.Build([Producer(), Consumer()], new CancellationToken(true));
        cancelled.Should().Throw<OperationCanceledException>();
        var large = () => _discovery.Build(Enumerable.Range(0, 2001).Select(i => Component("c" + i)).ToArray());
        large.Should().Throw<ArgumentException>().WithMessage("*2,000*");
    }
}
