using IntegrationHub.Domain;
using IntegrationHub.Tests;
namespace IntegrationHub.Domain.Tests;

[TestFixture]
public sealed class GraphTests
{
    private readonly IntegrationGraphBuilder _builder = new();
    [Test]
    public void Sequential_flow_has_correct_boundaries()
    {
        var graph = _builder.Build(TestDefinitions.Flow("flow", ("a", "b"), ("b", "c")));
        graph.EntryNodes.Should().Equal("a"); graph.ExitNodes.Should().Equal("c"); graph.ConnectedComponents.Should().ContainSingle(); graph.Edges.Should().HaveCount(2);
    }
    [Test]
    public void Fan_out_and_fan_in_are_preserved()
    {
        var graph = _builder.Build(TestDefinitions.Flow("flow", ("a", "b"), ("a", "c"), ("b", "d"), ("c", "d")));
        graph.Nodes.Should().HaveCount(4); graph.Edges.Should().HaveCount(4); graph.EntryNodes.Should().Equal("a"); graph.ExitNodes.Should().Equal("d");
    }
    [Test]
    public void Fan_in_warns_about_multiple_roots_without_rejecting_definition()
    {
        var validation = Validate(TestDefinitions.Flow("flow", ("a", "c"), ("b", "c")));
        validation.IsValid.Should().BeTrue(); validation.Issues.Should().Contain(i => i.Code == "MULTIPLE_ROOTS");
    }
    [Test]
    public void Cyclic_flow_is_valid_with_warnings()
    {
        var result = Validate(TestDefinitions.Flow("flow", ("a", "b"), ("b", "c"), ("c", "a")));
        result.IsValid.Should().BeTrue(); result.Issues.Select(i => i.Code).Should().Contain(["CYCLE", "NO_SOURCE", "NO_DESTINATION"]);
    }
    [Test]
    public void Explicit_boundaries_support_cyclic_flows()
    {
        var graph = _builder.Build(TestDefinitions.Flow("flow", ("a", "b"), ("b", "a")) with { Sources = ["a"], Destinations = ["b"] });
        graph.EntryNodes.Should().Equal("a"); graph.ExitNodes.Should().Equal("b");
    }
    [Test]
    public void Duplicate_nodes_are_structured_errors()
    {
        var d = TestDefinitions.Flow(); var result = Validate(d with { Nodes = d.Nodes.Append(d.Nodes[0]).ToArray() });
        result.IsValid.Should().BeFalse(); result.Issues.Should().Contain(i => i.Code == "DUPLICATE_NODE" && i.NodeId == "a");
    }
    [Test]
    public void Missing_references_and_duplicate_edges_are_errors()
    {
        var d = TestDefinitions.Flow(); var edge = d.Edges[0] with { ToNodeId = "missing" };
        var result = Validate(d with { Edges = [edge, edge] });
        result.IsValid.Should().BeFalse(); result.Issues.Select(i => i.Code).Should().Contain(["UNKNOWN_NODE", "DUPLICATE_EDGE", "DUPLICATE_EDGE_ID"]);
    }
    [Test]
    public void Disconnected_nodes_are_warnings()
    {
        var d = TestDefinitions.Flow(); var result = Validate(d with { Nodes = d.Nodes.Append(new IntegrationNode { Id = "c", Name = "C" }).ToArray() });
        result.IsValid.Should().BeTrue(); result.Issues.Select(i => i.Code).Should().Contain(["ORPHAN_NODE", "DISCONNECTED_COMPONENTS"]);
    }
    [Test]
    public void Self_edge_is_warning()
    {
        Validate(TestDefinitions.Flow("flow", ("a", "a"))).Issues.Should().Contain(i => i.Code == "SELF_EDGE" && i.Severity == ValidationSeverity.Warning);
    }
    [Test]
    public void Unknown_messages_and_invalid_producers_are_errors()
    {
        var d = TestDefinitions.Flow(); var result = Validate(d with { Edges = [d.Edges[0] with { MessageName = "Unknown" }], Messages = [new() { Name = "Known", Producer = "missing", Consumers = ["b"] }] });
        result.Issues.Select(i => i.Code).Should().Contain(["UNDEFINED_MESSAGE", "INVALID_MESSAGE_REFERENCE"]);
    }
    [Test]
    public void Invalid_group_hierarchy_is_rejected()
    {
        var d = TestDefinitions.Flow() with { Groups = [new("x", "X", "y"), new("y", "Y", "x")] };
        Validate(d).Issues.Should().Contain(i => i.Code == "GROUP_CYCLE" && i.Severity == ValidationSeverity.Error);
    }
    [Test]
    public void Custom_nodes_require_explicit_type_name()
    {
        var d = TestDefinitions.Flow();
        Validate(d with { Nodes = [d.Nodes[0] with { Type = NodeType.Custom }, d.Nodes[1]] }).Issues.Should().Contain(i => i.Code == "CUSTOM_TYPE_REQUIRED");
    }
    [Test]
    public void Traversal_follows_a_long_chain_without_recursion()
    {
        var adjacency = Enumerable.Range(0, 10000).ToDictionary(i => i.ToString(), i => new HashSet<string> { (i + 1).ToString() });
        var result = GraphTraversal.Reachable("0", adjacency);
        result.Should().HaveCount(10000).And.Contain("10000").And.NotContain("0");
    }
    [Test]
    public void Traversal_terminates_on_cycles_and_excludes_start()
    {
        var adjacency = new Dictionary<string, HashSet<string>> { ["a"] = ["b"], ["b"] = ["c"], ["c"] = ["a"] };
        GraphTraversal.Reachable("a", adjacency).Should().BeEquivalentTo(["b", "c"]);
    }
    [Test]
    public void Bidirectional_edges_create_two_arcs() => GraphTraversal.Arcs([new() { FromNodeId = "a", ToNodeId = "b", Direction = EdgeDirection.Bidirectional }]).Should().BeEquivalentTo(new[] { ("a", "b"), ("b", "a") });
    private ValidationResult Validate(IntegrationDefinition definition) => new IntegrationGraphValidator(_builder).Validate(definition);
}
