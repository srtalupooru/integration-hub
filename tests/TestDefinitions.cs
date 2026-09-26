using IntegrationHub.Domain;
namespace IntegrationHub.Tests;
internal static class TestDefinitions
{
    public static IntegrationDefinition Flow(string id = "test-flow", params (string From, string To)[] arcs)
    {
        if (arcs.Length == 0) arcs = [("a", "b")];
        var nodes = arcs.SelectMany(a => new[] { a.From, a.To }).Distinct().Select(id => new IntegrationNode { Id = id, Name = id.ToUpperInvariant(), Type = NodeType.InternalSystem }).ToArray();
        return new() { Id = new(id), Name = "Test flow", Version = "1.0", Status = IntegrationStatus.Draft, Nodes = nodes, Edges = arcs.Select((a, i) => new IntegrationEdge { Id = $"e{i}", FromNodeId = a.From, ToNodeId = a.To }).ToArray() };
    }
    public const string Json = """
    {"integration":{"id":"test-flow","name":"Test flow","version":"1.0","status":"Draft","nodes":[{"id":"a","name":"A","type":"InternalSystem"},{"id":"b","name":"B","type":"Api"}],"edges":[{"from":"a","to":"b","label":"Request"}]}}
    """;
    public const string Yaml = """
    integration:
      id: test-flow
      name: Test flow
      version: 1.0
      status: Draft
      nodes:
        - id: a
          name: A
          type: InternalSystem
        - id: b
          name: B
          type: Api
      edges:
        - from: a
          to: b
          label: Request
    """;
}
