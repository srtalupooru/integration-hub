namespace IntegrationHub.Domain;

public enum ValidationSeverity { Error, Warning }
public enum ValidationLevel { Syntax, Schema, Semantic }
public sealed record ValidationIssue(string Code, ValidationSeverity Severity, string Message,
    ValidationLevel Level = ValidationLevel.Semantic, string? NodeId = null, string? EdgeId = null,
    string? SuggestedResolution = null, string? Path = null);
public sealed record ValidationResult(IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsValid => Issues.All(i => i.Severity != ValidationSeverity.Error);
    public static ValidationResult Success => new([]);
}
public sealed record IntegrationGraph(
    IReadOnlyList<IntegrationNode> Nodes, IReadOnlyList<IntegrationEdge> Edges,
    IReadOnlyList<string> EntryNodes, IReadOnlyList<string> ExitNodes,
    IReadOnlyList<IReadOnlyList<string>> ConnectedComponents,
    IReadOnlyList<IntegrationDependency> Dependencies, IReadOnlyList<LogicalGroup> Groups);

public interface IIntegrationGraphBuilder { IntegrationGraph Build(IntegrationDefinition definition); }
public sealed class IntegrationGraphBuilder : IIntegrationGraphBuilder
{
    public IntegrationGraph Build(IntegrationDefinition definition)
    {
        var ids = definition.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        var edges = definition.Edges.Where(e => ids.Contains(e.FromNodeId) && ids.Contains(e.ToNodeId)).ToArray();
        var arcs = GraphTraversal.Arcs(edges).ToArray();
        var incoming = arcs.Select(a => a.To).ToHashSet();
        var outgoing = arcs.Select(a => a.From).ToHashSet();
        var remaining = new HashSet<string>(ids);
        var adjacency = ids.ToDictionary(id => id, _ => new HashSet<string>());
        foreach (var (from, to) in arcs) { adjacency[from].Add(to); adjacency[to].Add(from); }
        var components = new List<IReadOnlyList<string>>();
        while (remaining.Count > 0)
        {
            var start = remaining.First();
            var component = GraphTraversal.Reachable(start, adjacency);
            component.Add(start);
            remaining.ExceptWith(component);
            components.Add(component.Order(StringComparer.Ordinal).ToArray());
        }
        return new(definition.Nodes, edges,
            definition.Sources.Count > 0 ? definition.Sources : ids.Where(id => !incoming.Contains(id)).Order().ToArray(),
            definition.Destinations.Count > 0 ? definition.Destinations : ids.Where(id => !outgoing.Contains(id)).Order().ToArray(),
            components, definition.Dependencies, definition.Groups);
    }
}

public static class GraphTraversal
{
    public static IEnumerable<(string From, string To)> Arcs(IEnumerable<IntegrationEdge> edges)
    {
        foreach (var edge in edges)
        {
            yield return (edge.FromNodeId, edge.ToNodeId);
            if (edge.Direction == EdgeDirection.Bidirectional) yield return (edge.ToNodeId, edge.FromNodeId);
        }
    }
    public static HashSet<string> Reachable(string start, IReadOnlyDictionary<string, HashSet<string>> adjacency)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { start };
        var queue = new Queue<string>(); queue.Enqueue(start);
        while (queue.TryDequeue(out var current))
            if (adjacency.TryGetValue(current, out var next))
                foreach (var id in next) if (visited.Add(id)) queue.Enqueue(id);
        visited.Remove(start);
        return visited;
    }
    public static bool HasCycle(IEnumerable<string> ids, IEnumerable<(string From, string To)> arcs)
    {
        var indegrees = ids.Distinct().ToDictionary(id => id, _ => 0);
        var adjacency = indegrees.Keys.ToDictionary(id => id, _ => new List<string>());
        foreach (var (from, to) in arcs)
            if (adjacency.ContainsKey(from) && indegrees.ContainsKey(to)) { adjacency[from].Add(to); indegrees[to]++; }
        var queue = new Queue<string>(indegrees.Where(p => p.Value == 0).Select(p => p.Key));
        var count = 0;
        while (queue.TryDequeue(out var node))
        {
            count++;
            foreach (var next in adjacency[node]) if (--indegrees[next] == 0) queue.Enqueue(next);
        }
        return count < indegrees.Count;
    }
}

public sealed class IntegrationGraphValidator(IIntegrationGraphBuilder builder)
{
    public ValidationResult Validate(IntegrationDefinition definition)
    {
        var issues = new List<ValidationIssue>();
        void Add(string code, string message, bool error = true, string? node = null, string? edge = null, string? fix = null) =>
            issues.Add(new(code, error ? ValidationSeverity.Error : ValidationSeverity.Warning, message, NodeId: node, EdgeId: edge, SuggestedResolution: fix));
        var ids = definition.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var duplicate in definition.Nodes.GroupBy(n => n.Id).Where(g => g.Count() > 1))
            Add("DUPLICATE_NODE", $"Node '{duplicate.Key}' occurs more than once.", node: duplicate.Key, fix: "Give every node a unique ID.");
        var groupIds = definition.Groups.Select(g => g.Id).ToHashSet();
        foreach (var group in definition.Groups.GroupBy(g => g.Id).Where(g => g.Count() > 1)) Add("DUPLICATE_GROUP", $"Group '{group.Key}' occurs more than once.");
        foreach (var group in definition.Groups.Where(g => g.ParentId is not null && !groupIds.Contains(g.ParentId!))) Add("UNKNOWN_GROUP", $"Parent of group '{group.Id}' does not exist.");
        if (GraphTraversal.HasCycle(groupIds, definition.Groups.Where(g => g.ParentId is not null).Select(g => (g.ParentId!, g.Id)))) Add("GROUP_CYCLE", "Logical groups cannot contain a cycle.");
        foreach (var node in definition.Nodes)
        {
            if (node.GroupId is not null && !groupIds.Contains(node.GroupId)) Add("UNKNOWN_GROUP", "Node references an unknown group.", node: node.Id);
            if (node.Type == NodeType.Custom && string.IsNullOrWhiteSpace(node.CustomType)) Add("CUSTOM_TYPE_REQUIRED", "Custom nodes require customType.", node: node.Id);
        }
        foreach (var id in definition.Sources.Concat(definition.Destinations).Where(id => !ids.Contains(id))) Add("UNKNOWN_BOUNDARY", $"Source or destination '{id}' does not exist.", node: id);
        foreach (var duplicate in definition.Edges.GroupBy(e => e.Id).Where(g => g.Count() > 1)) Add("DUPLICATE_EDGE_ID", $"Edge ID '{duplicate.Key}' occurs more than once.", edge: duplicate.Key);
        foreach (var duplicate in definition.Edges.GroupBy(e => (e.FromNodeId, e.ToNodeId, e.Label)).Where(g => g.Count() > 1)) Add("DUPLICATE_EDGE", "A relationship is repeated.", edge: duplicate.First().Id);
        var messages = definition.AllMessages.ToArray();
        var messageNames = messages.Select(m => m.Name).ToHashSet();
        foreach (var duplicate in messages.GroupBy(m => m.Name).Where(g => g.Count() > 1)) Add("DUPLICATE_MESSAGE", $"Message '{duplicate.Key}' occurs more than once.");
        foreach (var edge in definition.Edges)
        {
            if (!ids.Contains(edge.FromNodeId) || !ids.Contains(edge.ToNodeId)) Add("UNKNOWN_NODE", "Edge endpoint does not exist.", edge: edge.Id, fix: "Use a node ID from nodes.");
            if (edge.FromNodeId == edge.ToNodeId) Add("SELF_EDGE", "Edge references the same node twice.", false, edge: edge.Id);
            if (edge.MessageName is not null && !messageNames.Contains(edge.MessageName)) Add("UNDEFINED_MESSAGE", $"Message '{edge.MessageName}' is undefined.", edge: edge.Id);
        }
        foreach (var message in messages)
            foreach (var reference in message.Consumers.Prepend(message.Producer))
                if (!ids.Contains(reference)) Add("INVALID_MESSAGE_REFERENCE", $"Message '{message.Name}' references unknown node '{reference}'.", node: reference, fix: "Use node IDs for producers and consumers.");
        foreach (var dependency in definition.Dependencies)
        {
            if (dependency.IntegrationId == definition.Id.Value) Add("SELF_DEPENDENCY", "Integration cannot depend on itself.");
            try { _ = new IntegrationId(dependency.IntegrationId); }
            catch (ArgumentException) { Add("INVALID_DEPENDENCY", "Dependency ID must be a valid integration slug."); }
        }
        if (definition.Dependencies.GroupBy(d => d.IntegrationId).Any(g => g.Count() > 1)) Add("DUPLICATE_DEPENDENCY", "Integration dependency occurs more than once.");
        var graph = builder.Build(definition);
        foreach (var component in graph.ConnectedComponents.Where(c => c.Count == 1))
            if (!graph.Edges.Any(e => e.FromNodeId == component[0] || e.ToNodeId == component[0])) Add("ORPHAN_NODE", "Node has no relationships.", false, component[0], fix: "Connect it or document why it is intentionally isolated.");
        if (graph.ConnectedComponents.Count > 1) Add("DISCONNECTED_COMPONENTS", "Integration contains disconnected components.", false);
        if (graph.EntryNodes.Count == 0) Add("NO_SOURCE", "No source can be inferred. Declare sources for cyclic flows.", false);
        if (graph.ExitNodes.Count == 0) Add("NO_DESTINATION", "No destination can be inferred. Declare destinations for cyclic flows.", false);
        if (graph.EntryNodes.Count > 1) Add("MULTIPLE_ROOTS", "Integration has multiple entry nodes.", false);
        if (GraphTraversal.HasCycle(ids, GraphTraversal.Arcs(graph.Edges))) Add("CYCLE", "Flow contains a cycle; traversal remains safe.", false, fix: "Confirm that the cycle is intentional.");
        return new(issues);
    }
}
