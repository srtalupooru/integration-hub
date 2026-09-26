using IntegrationHub.Domain;
namespace IntegrationHub.Application;
public sealed class GlobalGraphQueries(IGlobalIntegrationGraphService graphs, IDiagramGenerator diagrams)
{
    public async Task<string> GetDiagramAsync(CancellationToken ct)
    {
        var graph = await graphs.BuildAsync(ct);
        var ids = graph.Nodes.Select(n => n.Id).ToHashSet();
        var nodes = graph.Nodes.Select(n => new IntegrationNode { Id = n.Id, Name = n.Name, Type = Enum.TryParse<NodeType>(n.Kind, out var type) ? type : NodeType.Custom }).ToArray();
        var edges = graph.Edges.Where(e => ids.Contains(e.From) && ids.Contains(e.To)).Select((e, i) => new IntegrationEdge { Id = $"global-{i}", FromNodeId = e.From, ToNodeId = e.To, Label = e.Kind, Mode = e.Mode }).ToArray();
        return diagrams.Generate(new(nodes, edges, [], [], [], [], []));
    }
}
