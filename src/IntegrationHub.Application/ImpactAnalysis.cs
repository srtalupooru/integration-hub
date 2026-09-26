using IntegrationHub.Contracts;
using IntegrationHub.Domain;
namespace IntegrationHub.Application;

public sealed class GlobalIntegrationGraphService(IIntegrationRepository repository, IComponentRepository? components = null, ComponentDiscovery? discovery = null) : IGlobalIntegrationGraphService
{
    public static string NodeKey(string integrationId, IntegrationNode node) => node.SystemId is not null ? $"system:{node.SystemId}" : node.SharedResourceId is not null ? $"resource:{node.SharedResourceId}" : $"node:{integrationId}:{node.Id}";
    public async Task<GlobalGraph> BuildAsync(CancellationToken ct)
    {
        var definitions = await repository.GetDefinitionsAsync(ct);
        var nodes = new Dictionary<string, GlobalNode>(StringComparer.Ordinal);
        var edges = new List<GlobalEdge>();
        void Add(string id, string name, string kind, string integrationId)
        {
            if (nodes.TryGetValue(id, out var existing)) nodes[id] = existing with { IntegrationIds = existing.IntegrationIds.Append(integrationId).Distinct().Order().ToArray() };
            else nodes[id] = new(id, name, kind, [integrationId]);
        }
        foreach (var definition in definitions)
        {
            var id = definition.Id.Value;
            Add($"integration:{id}", definition.Name, "Integration", id);
            var keys = definition.Nodes.ToDictionary(n => n.Id, n => NodeKey(id, n));
            foreach (var node in definition.Nodes) Add(keys[node.Id], node.Name, node.SystemId is null ? node.Type.ToString() : "System", id);
            foreach (var (from, to) in GraphTraversal.Arcs(definition.Edges)) edges.Add(new(keys[from], keys[to], "Flow", id));
            foreach (var message in definition.AllMessages)
            {
                var key = $"message:{message.Name}";
                Add(key, message.Name, "Message", id);
                edges.Add(new(keys[message.Producer], key, "Produces", id));
                foreach (var consumer in message.Consumers) edges.Add(new(key, keys[consumer], "Consumes", id));
            }
            foreach (var dependency in definition.Dependencies)
                edges.Add(new($"integration:{dependency.IntegrationId}", $"integration:{id}", "Dependency", id));
        }
        if (components is not null && discovery is not null)
        {
            var records = await components.ListAsync(false, ct);
            var generated = discovery.Build(records, ct);
            foreach (var component in records.Where(c => c.Definition.Status != IntegrationStatus.Retired))
                nodes[$"component:{component.Definition.Id}"] = new($"component:{component.Definition.Id}", component.Definition.Name, component.Definition.Type.ToString(), []);
            foreach (var integration in generated.Integrations)
            {
                // This namespace cannot collide with authored integration IDs (which are slugs).
                var id = $"discovered:{integration.Id}";
                Add($"integration:{id}", integration.Name, "Integration", id);
                foreach (var source in integration.Sources)
                    Add($"component:{source.Id}", source.Name, records.Single(c => c.Definition.Id == source.Id).Definition.Type.ToString(), id);
                foreach (var connection in integration.Connections)
                    edges.Add(new($"component:{connection.ProducerId}", $"component:{connection.ConsumerId}", connection.Kind == ComponentInteractionKind.HttpCall ? $"Calls {connection.HttpMethod ?? "HTTP"} {connection.HttpPath}" : $"{connection.MessageType}: {connection.Contract} v{connection.Version} ({connection.Delivery})", id, connection.Channel.Kind == ChannelKind.Http ? InteractionMode.Synchronous : InteractionMode.Asynchronous));
            }
        }
        // Declared dependencies connect integration identities; membership is not a causal flow edge.
        return new(nodes.Values.OrderBy(n => n.Id).ToArray(), edges.Distinct().ToArray());
    }
}
public sealed class ImpactAnalysisService(IGlobalIntegrationGraphService graphs) : IImpactAnalysisService
{
    public async Task<ImpactResult> AnalyzeAsync(string componentId, CancellationToken ct)
    {
        var graph = await graphs.BuildAsync(ct);
        if (!graph.Nodes.Any(n => n.Id == componentId)) throw new NotFoundException($"Component '{componentId}' was not found.");
        var downstream = Traverse(graph, componentId, false);
        var upstream = Traverse(graph, componentId, true);
        var direct = graph.Nodes.Single(n => n.Id == componentId).IntegrationIds;
        var affected = direct.Concat(downstream.SelectMany(n => n.IntegrationIds)).ToHashSet();
        // Expand declared inter-integration dependencies separately, so unrelated branches are not fabricated.
        var dependencyAdjacency = graph.Edges.Where(e => e.Kind == "Dependency").GroupBy(e => e.From).ToDictionary(g => g.Key, g => g.Select(e => e.To).ToHashSet());
        foreach (var id in affected.ToArray())
            foreach (var dependent in GraphTraversal.Reachable($"integration:{id}", dependencyAdjacency)) affected.Add(dependent["integration:".Length..]);
        return new(componentId, upstream, downstream, direct, affected.Order().ToArray(), downstream.Where(n => n.Kind is "System" or "ExternalSystem" or "InternalSystem" or "SaaS").ToArray());
    }
    private static IReadOnlyList<GlobalNode> Traverse(GlobalGraph graph, string start, bool reverse)
    {
        var adjacency = graph.Edges.GroupBy(e => reverse ? e.To : e.From).ToDictionary(g => g.Key, g => g.Select(e => reverse ? e.From : e.To).ToHashSet());
        var reachable = GraphTraversal.Reachable(start, adjacency);
        return graph.Nodes.Where(n => reachable.Contains(n.Id)).OrderBy(n => n.Id).ToArray();
    }
    public async Task<IReadOnlyList<GlobalNode>> GetUpstreamDependencies(string componentId, CancellationToken ct) => (await AnalyzeAsync(componentId, ct)).Upstream;
    public async Task<IReadOnlyList<GlobalNode>> GetDownstreamDependencies(string componentId, CancellationToken ct) => (await AnalyzeAsync(componentId, ct)).Downstream;
    public async Task<IReadOnlyList<string>> GetAffectedIntegrations(string componentId, CancellationToken ct) => (await AnalyzeAsync(componentId, ct)).AffectedIntegrations;
    public async Task<IReadOnlyList<GlobalNode>> GetDependentSystems(string componentId, CancellationToken ct) => (await AnalyzeAsync(componentId, ct)).DependentSystems;
    public async Task<IReadOnlyList<string>> GetDependencies(string integrationId, CancellationToken ct) => (await GetUpstreamDependencies($"integration:{integrationId}", ct)).Where(n => n.Kind == "Integration").Select(n => n.Id["integration:".Length..]).ToArray();
}
