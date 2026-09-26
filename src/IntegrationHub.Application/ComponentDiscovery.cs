using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IntegrationHub.Contracts;
using IntegrationHub.Domain;
namespace IntegrationHub.Application;

// Pure, deterministic derivation: no writes, timers, network schema fetches or probabilistic matching.
public sealed class ComponentDiscovery
{
    private sealed record Endpoint(ComponentDetail Component, MessageBinding Binding);
    private sealed record Route(string Environment, string Contract, string Version, MessageType Type, MessageChannel Channel);
    private static Route Key(Endpoint e) => new(e.Component.Definition.Environment, e.Binding.Contract, e.Binding.Version, e.Binding.MessageType, e.Binding.Channel);
    private static string Hash(params string[] parts) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parts)))).ToLowerInvariant();
    public DiscoveryResult Build(IReadOnlyList<ComponentDetail> records, CancellationToken ct = default)
    {
        var active = records.Where(r => !r.IsArchived).OrderBy(r => r.Definition.Id, StringComparer.Ordinal).ToArray();
        if (active.Length > 2000 || active.Sum(c => c.Definition.Consumes.Count + c.Definition.Publishes.Count + c.Definition.Sends.Count + c.Definition.Endpoints.Count + c.Definition.Calls.Count) > 20000)
            throw new ArgumentException("Discovery currently supports up to 2,000 active components and 20,000 bindings. Split the catalogue before exceeding these limits.");
        var fingerprint = Hash(active.SelectMany(c => new[] { c.Definition.Id, c.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture), c.DefinitionHash }).ToArray());
        var components = active.Where(c => c.Definition.Status != IntegrationStatus.Retired).ToArray();
        ct.ThrowIfCancellationRequested();
        var publishers = components.SelectMany(c => c.Definition.Publishes.Concat(c.Definition.Sends).Select(b => new Endpoint(c, b))).ToArray();
        var consumers = components.SelectMany(c => c.Definition.Consumes.Select(b => new Endpoint(c, b))).ToArray();
        var consumersByRoute = consumers.GroupBy(Key).ToDictionary(g => g.Key, g => g.ToArray());
        var deliveryGroups = consumers.GroupBy(c => (Key(c), c.Binding.Subscription)).ToDictionary(g => g.Key, g => g.Count());
        var publishersByContract = publishers.GroupBy(p => p.Binding.Contract).ToDictionary(g => g.Key, g => g.ToArray());
        var issues = new List<DiscoveryIssue>();
        var links = new List<DiscoveredConnection>();
        var linkedPublications = new HashSet<(string, string)>();
        var linkedConsumptions = new Dictionary<(string, string), List<Endpoint>>();
        void Issue(string code, Endpoint e, string message, string resolution) => issues.Add(new(code, e.Component.Definition.Id, e.Binding.Id, message, resolution));
        foreach (var producer in publishers)
        {
            ct.ThrowIfCancellationRequested();
            var p = producer.Binding;
            var possible = consumersByRoute.GetValueOrDefault(Key(producer), []).Where(c =>
                (c.Binding.SourceComponent is null || c.Binding.SourceComponent == producer.Component.Definition.Id) &&
                (p.TargetComponent is null || p.TargetComponent == c.Component.Definition.Id)).ToArray();
            var candidates = possible.Where(c => c.Binding.ContentType == p.ContentType &&
                (p.SchemaFingerprint is null || c.Binding.SchemaFingerprint is null || p.SchemaFingerprint == c.Binding.SchemaFingerprint)).ToArray();
            if (p.TargetComponent is not null && possible.Length == 0)
                Issue("TARGET_NOT_FOUND", producer, $"No matching HTTP input on target component '{p.TargetComponent}'.", "Add or restore the target and align its consumed message route. The target selector never falls back to another receiver.");
            // A direct request must resolve to one documented destination, not guessed fan-out.
            if (p.Channel.Kind == ChannelKind.Http && candidates.Length > 1)
            {
                Issue("AMBIGUOUS_HTTP_TARGET", producer, "This HTTP publication has multiple matching destinations; no connection was created.", "Set targetComponent to the intended receiver, or correct the channel namespace/path.");
                continue;
            }
            foreach (var consumer in candidates)
            {
                if (links.Count >= 50000) throw new ArgumentException("Discovery exceeds 50,000 connections. Narrow component bindings or split the catalogue.");
                var c = consumer.Binding;
                var producerId = producer.Component.Definition.Id;
                var consumerId = consumer.Component.Definition.Id;
                var delivery = p.Channel.Kind == ChannelKind.Http ? "Direct" : p.Channel.Kind == ChannelKind.Queue || deliveryGroups[(Key(consumer), c.Subscription)] > 1 ? "Competing" : "Subscription";
                links.Add(new("link-" + Hash(producerId, p.Id, consumerId, c.Id), producerId, p.Id, consumerId, c.Id,
                    p.Contract, p.Version, p.MessageType, p.Channel, delivery, c.Subscription,
                    p.Channel.Kind == ChannelKind.Http ? ComponentInteractionKind.HttpCall : ComponentInteractionKind.Message,
                    HttpPath: p.Channel.Kind == ChannelKind.Http ? p.Channel.Name : null));
                linkedPublications.Add((producerId, p.Id));
                if (!linkedConsumptions.TryGetValue((consumerId, c.Id), out var matches)) linkedConsumptions[(consumerId, c.Id)] = matches = [];
                matches.Add(producer);
                if (p.SchemaFingerprint is null || c.SchemaFingerprint is null)
                    Issue("SCHEMA_UNVERIFIED", consumer, $"Schema compatibility with '{producerId}/{p.Id}' is not verified; the declared route matches.", "Optionally provide the same SHA-256 schemaFingerprint on both declarations. The catalogue does not infer payload compatibility.");
            }
        }
        foreach (var consumer in consumers)
        {
            var c = consumer.Binding;
            if (linkedConsumptions.TryGetValue((consumer.Component.Definition.Id, c.Id), out var matches))
            {
                if (matches.Count > 1) Issue("MULTIPLE_PUBLISHERS", consumer, $"{matches.Count} publications can supply this input. All matching publishers are shown.", "Use sourceComponent if only one publisher is part of this documented route.");
                continue;
            }
            var near = publishersByContract.GetValueOrDefault(c.Contract, []);
            var code = "UNRESOLVED_CONSUMER";
            if (near.Length > 0)
            {
                if (!near.Any(p => p.Component.Definition.Environment == consumer.Component.Definition.Environment)) code = "ENVIRONMENT_MISMATCH";
                else
                {
                    near = near.Where(p => p.Component.Definition.Environment == consumer.Component.Definition.Environment).ToArray();
                    if (!near.Any(p => p.Binding.Version == c.Version)) code = "VERSION_MISMATCH";
                    else if (!near.Any(p => p.Binding.Version == c.Version && p.Binding.Channel == c.Channel)) code = "CHANNEL_MISMATCH";
                    else if (!near.Any(p => Key(p) == Key(consumer))) code = "MESSAGE_TYPE_MISMATCH";
                    else if (near.Any(p => Key(p) == Key(consumer) && (p.Binding.ContentType != c.ContentType || p.Binding.SchemaFingerprint is not null && c.SchemaFingerprint is not null && p.Binding.SchemaFingerprint != c.SchemaFingerprint))) code = "PAYLOAD_MISMATCH";
                }
            }
            if (c.SourceComponent is not null && !publishersByContract.GetValueOrDefault(c.Contract, []).Any(p => p.Component.Definition.Id == c.SourceComponent && Key(p) == Key(consumer))) code = "SOURCE_NOT_FOUND";
            Issue(code, consumer, $"No resolved publisher for '{c.Contract}' v{c.Version} on {c.Channel.Namespace}/{c.Channel.Name} in {consumer.Component.Definition.Environment}.", "Add the publisher or align the exact environment, contract, version, type, channel, payload metadata and selectors. Review HTTP ambiguity findings. Unmatched inputs may represent external boundaries.");
        }
        foreach (var publisher in publishers.Where(p => !linkedPublications.Contains((p.Component.Definition.Id, p.Binding.Id))))
            Issue("UNCONSUMED_PUBLICATION", publisher, $"No resolved consumer for '{publisher.Binding.Contract}' v{publisher.Binding.Version}.", "Add a consuming component or verify its binding. Unconsumed outputs may represent external boundaries.");
        foreach (var group in consumers.GroupBy(c => (Key(c), c.Binding.Subscription)).Where(g => g.Count() > 1 && g.Key.Item1.Channel.Kind is ChannelKind.Queue or ChannelKind.Topic or ChannelKind.Stream))
            foreach (var consumer in group)
                Issue("COMPETING_CONSUMERS", consumer, "Several consumers share this queue, subscription or consumer group; a message is not guaranteed to reach every instance.", "Use different subscriptions/groups for independent fan-out. Edges show possible delivery, not an execution trace.");
        foreach (var component in components.Where(c => c.Definition.Status == IntegrationStatus.Deprecated))
            issues.Add(new("DEPRECATED_COMPONENT", component.Definition.Id, null, "A deprecated component still participates in discovery.", "Set status to Retired or archive the definition to remove it from discovery."));
        var byId = components.ToDictionary(c => c.Definition.Id, StringComparer.Ordinal);
        foreach (var component in components)
        {
            foreach (var warning in ComponentValidator.Validate(component.Definition).Issues.Where(i => i.Code.StartsWith("LEGACY_", StringComparison.Ordinal)))
                issues.Add(new(warning.Code, component.Definition.Id, warning.NodeId, warning.Message, "Update the source definition explicitly; saved source and history are never rewritten automatically."));
            foreach (var call in component.Definition.Calls)
            {
                ct.ThrowIfCancellationRequested();
                string? failure = null;
                ApiEndpointDefinition? endpoint = null;
                if (!byId.TryGetValue(call.TargetComponent, out var target)) failure = "HTTP_TARGET_NOT_FOUND";
                else if (target.Definition.Environment != component.Definition.Environment) failure = "HTTP_ENVIRONMENT_MISMATCH";
                else
                {
                    endpoint = target.Definition.Endpoints.SingleOrDefault(e => e.Id == call.Endpoint);
                    if (endpoint is null) failure = "HTTP_ENDPOINT_NOT_FOUND";
                    else if (endpoint.Method != call.Method) failure = "HTTP_METHOD_MISMATCH";
                    else if (endpoint.Version != call.Version) failure = "HTTP_VERSION_MISMATCH";
                }
                if (failure is not null)
                {
                    issues.Add(new(failure, component.Definition.Id, call.Id, $"HTTP call to '{call.TargetComponent}/{call.Endpoint}' ({call.Method}, v{call.Version}) is unresolved.",
                        "Declare that endpoint on the active target component with the same environment, method and version. Legacy HTTP message declarations must be updated explicitly to endpoints."));
                    continue;
                }
                if (links.Count >= 50000) throw new ArgumentException("Discovery exceeds 50,000 connections. Narrow component bindings or split the catalogue.");
                links.Add(new("link-" + Hash("http", component.Definition.Id, call.Id, call.TargetComponent, call.Endpoint), component.Definition.Id, call.Id,
                    call.TargetComponent, call.Endpoint, call.Endpoint, call.Version, MessageType.Request,
                    new() { Kind = ChannelKind.Http, Namespace = call.TargetComponent, Name = endpoint!.Path }, "Direct", null,
                    ComponentInteractionKind.HttpCall, call.Method.ToString(), endpoint.Path));
            }
        }
        var orderedLinks = links.OrderBy(l => l.Id, StringComparer.Ordinal).ToArray();
        var orderedIssues = issues.Distinct().OrderBy(i => i.ComponentId, StringComparer.Ordinal).ThenBy(i => i.BindingId, StringComparer.Ordinal).ThenBy(i => i.Code, StringComparer.Ordinal).ThenBy(i => i.Message, StringComparer.Ordinal).ToArray();
        var nodes = components.Select(c => Node(c.Definition)).ToArray();
        var edges = orderedLinks.Select(Edge).ToArray();
        var graph = new IntegrationGraphBuilder().Build(new() { Nodes = nodes, Edges = edges });
        var integrations = new List<DiscoveredIntegration>();
        var linkedIds = links.SelectMany(l => new[] { l.ProducerId, l.ConsumerId }).ToHashSet(StringComparer.Ordinal);
        foreach (var members in graph.ConnectedComponents.Where(m => m.Any(linkedIds.Contains)))
        {
            ct.ThrowIfCancellationRequested();
            var ids = members.ToHashSet(StringComparer.Ordinal);
            var componentRecords = members.Select(id => byId[id]).ToArray();
            var connections = orderedLinks.Where(l => ids.Contains(l.ProducerId)).ToArray();
            var componentEdges = connections.Select(Edge).ToArray();
            var environment = componentRecords[0].Definition.Environment;
            // Membership identifies the network. Revision-only edits keep its ID; merges/splits produce new IDs.
            var id = "auto-" + Hash(members.ToArray());
            var name = string.Join(" · ", componentRecords.Take(3).Select(c => c.Definition.Name)) + (members.Count > 3 ? $" +{members.Count - 3}" : "");
            if (name.Length > 256) name = name[..253] + "…";
            var sources = componentRecords.Select(c => new ComponentSource(c.Definition.Id, c.Definition.Name, c.Revision, c.DefinitionHash)).ToArray();
            var hasCycle = GraphTraversal.HasCycle(members, componentEdges.Select(e => (e.FromNodeId, e.ToNodeId)));
            var localIssues = orderedIssues.Where(i => ids.Contains(i.ComponentId)).ToList();
            if (hasCycle) localIssues.Add(new("CYCLIC_FLOW", members[0], null, "This discovered network contains a cycle, including possible self-delivery.", "Review intentional feedback/retry routes. A cyclic network may have no root or terminal component."));
            var messages = connections.Where(l => l.Kind == ComponentInteractionKind.Message).GroupBy(l => (l.ProducerId, l.PublishBindingId)).Select(group =>
            {
                var publisher = byId[group.Key.ProducerId].Definition;
                var binding = publisher.Publishes.Concat(publisher.Sends).Single(b => b.Id == group.Key.PublishBindingId);
                return new IntegrationMessage { Name = binding.Contract + "@" + binding.Version + "#" + Hash(group.Key.ProducerId, group.Key.PublishBindingId)[..16],
                    Type = binding.MessageType, Version = binding.Version, Producer = group.Key.ProducerId,
                    Consumers = group.Select(l => l.ConsumerId).Distinct().Order(StringComparer.Ordinal).ToArray(),
                    Description = binding.Description, ContentType = binding.ContentType, Schema = binding.SchemaFingerprint is null ? "" : "sha256:" + binding.SchemaFingerprint,
                    TopicOrQueue = binding.Channel.Namespace + "/" + binding.Channel.Name };
            }).ToArray();
            var definition = new IntegrationDefinition { Id = new(id), Name = name, Environment = environment,
                Description = "Automatically discovered from component HTTP calls, sent commands and published/consumed messages. Connections represent possible communication, not proven input-to-output causality or a runtime execution sequence.",
                Status = componentRecords.All(c => c.Definition.Status == IntegrationStatus.Production) ? IntegrationStatus.Production : IntegrationStatus.Draft,
                Criticality = componentRecords.Max(c => c.Definition.Criticality),
                BusinessDomain = Summary(componentRecords.Select(c => c.Definition.Domain), "Multiple domains"),
                Ownership = new(Summary(componentRecords.Select(c => c.Definition.Owner), "Multiple component owners")),
                Tags = ["discovered"], Nodes = componentRecords.Select(c => Node(c.Definition)).ToArray(), Edges = componentEdges, Messages = messages,
                Metadata = new Dictionary<string, string> { ["origin"] = "component-discovery", ["snapshot"] = fingerprint,
                    ["sourceRevisions"] = string.Join(", ", sources.Select(s => $"{s.Id}@{s.Revision}")), ["readOnly"] = "true" } };
            integrations.Add(new(id, name, environment, sources, definition, connections, localIssues, hasCycle));
        }
        return new(fingerprint, integrations.OrderBy(i => i.Id, StringComparer.Ordinal).ToArray(),
            orderedIssues.Concat(integrations.SelectMany(i => i.Issues).Where(i => i.Code == "CYCLIC_FLOW")).ToArray(),
            components.Select(c => c.Definition.Id).Where(id => !linkedIds.Contains(id)).ToArray(),
            active.Where(c => c.Definition.Status == IntegrationStatus.Retired).Select(c => c.Definition.Id).ToArray());
    }
    private static string Summary(IEnumerable<string> values, string fallback)
    {
        var text = string.Join(", ", values.Where(v => v.Length > 0).Distinct().Order(StringComparer.Ordinal));
        return text.Length <= 256 ? text : fallback;
    }
    private static IntegrationNode Node(ComponentDefinition c) => new() { Id = c.Id, Name = c.Name, Type = c.Type, Environment = c.Environment,
        Description = c.Description, Technology = c.Technology, Owner = c.Owner, Metadata = new Dictionary<string, string> { ["componentId"] = c.Id } };
    private static IntegrationEdge Edge(DiscoveredConnection c) => new() { Id = c.Id, FromNodeId = c.ProducerId, ToNodeId = c.ConsumerId,
        Label = c.Kind == ComponentInteractionKind.HttpCall ? $"{c.HttpMethod ?? "HTTP"} {c.HttpPath} (v{c.Version})" : $"{c.MessageType}: {c.Contract} v{c.Version}" + (c.Delivery == "Competing" ? " (competing)" : ""),
        Description = $"{c.ProducerId}/{c.PublishBindingId} → {c.ConsumerId}/{c.ConsumeBindingId}; {c.Channel.Namespace}/{c.Channel.Name}; {c.Delivery}; subscription: {c.Subscription ?? "n/a"}",
        TransportType = c.Channel.Kind.ToString(), Protocol = c.Channel.Kind == ChannelKind.Http ? "HTTP" : c.Channel.Kind.ToString(),
        Mode = c.Channel.Kind == ChannelKind.Http ? InteractionMode.Synchronous : InteractionMode.Asynchronous };
}
