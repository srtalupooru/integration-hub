using IntegrationHub.Contracts;
using IntegrationHub.Domain;
using IntegrationHub.Tests;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
namespace IntegrationHub.Application.Tests;

[TestFixture]
public sealed class ApplicationTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    [TestCase(NodeType.ExternalSystem, "system")]
    [TestCase(NodeType.InternalSystem, "system")]
    [TestCase(NodeType.SaaS, "system")]
    [TestCase(NodeType.Api, "api")]
    [TestCase(NodeType.ApiManagement, "api")]
    [TestCase(NodeType.ServiceBusTopic, "messaging")]
    [TestCase(NodeType.ServiceBusQueue, "messaging")]
    [TestCase(NodeType.MessageBroker, "messaging")]
    [TestCase(NodeType.EventGrid, "messaging")]
    [TestCase(NodeType.AzureFunction, "compute")]
    [TestCase(NodeType.LogicApp, "compute")]
    [TestCase(NodeType.Library, "compute")]
    [TestCase(NodeType.NuGetPackage, "compute")]
    [TestCase(NodeType.Transformation, "compute")]
    [TestCase(NodeType.Database, "data")]
    [TestCase(NodeType.Storage, "data")]
    [TestCase(NodeType.Sftp, "data")]
    [TestCase(NodeType.FileShare, "data")]
    [TestCase(NodeType.ManualProcess, "other")]
    [TestCase(NodeType.Custom, "other")]
    public void Mermaid_colours_component_types_without_losing_optional_styling(NodeType type, string category)
    {
        var d = TestDefinitions.Flow();
        d = d with { Nodes = [d.Nodes[0] with { Type = type, Optional = true }, d.Nodes[1]] };
        var output = new MermaidDiagramGenerator().Generate(new IntegrationGraphBuilder().Build(d));
        output.Should().Contain($"class n0 {category}").And.Contain($"classDef {category} fill:")
            .And.Contain($"class n0 hub-type-{type}").And.Contain("style n0 stroke-dasharray: 5 5");
    }

    [Test]
    public void Workflow_cards_wrap_long_labels_safely_and_preserve_connection_direction()
    {
        var d = TestDefinitions.Flow();
        d = d with { Nodes = [d.Nodes[0] with { Name = new string('a', 60) + "<script> 😀", Type = NodeType.Api }, d.Nodes[1]] };
        var generator = new MermaidDiagramGenerator();
        var output = generator.Generate(new IntegrationGraphBuilder().Build(d));
        output.Should().Contain("<br/>").And.NotContain("<script>").And.Contain("#128512;")
            .And.Contain("class n0 hub-output").And.NotContain("class n0 hub-input")
            .And.Contain("class n1 hub-input").And.NotContain("class n1 hub-output");
        d = d with { Edges = [d.Edges[0] with { Direction = EdgeDirection.Bidirectional }] };
        output = generator.Generate(new IntegrationGraphBuilder().Build(d));
        output.Should().Contain("class n0 hub-input").And.Contain("class n1 hub-output").And.Contain("<-->");
    }

    [Test]
    public void Mermaid_escapes_untrusted_labels_and_does_not_use_author_ids()
    {
        var d = TestDefinitions.Flow(); d = d with { Nodes = [d.Nodes[0] with { Name = "\"<script>|end" }, d.Nodes[1]] };
        var output = new MermaidDiagramGenerator().Generate(new IntegrationGraphBuilder().Build(d));
        output.Should().Contain("n0[").And.Contain("#34;").And.NotContain("<script>");
    }
    [Test]
    public void Mermaid_supports_nested_groups_and_optional_async_nodes()
    {
        var d = TestDefinitions.Flow(); d = d with { Groups = [new("parent", "Parent"), new("child", "Child", "parent")], Nodes = [d.Nodes[0] with { GroupId = "child", Optional = true }, d.Nodes[1]], Edges = [d.Edges[0] with { Mode = InteractionMode.Asynchronous }] };
        var output = new MermaidDiagramGenerator().Generate(new IntegrationGraphBuilder().Build(d));
        output.Should().Contain("subgraph g0").And.Contain("subgraph g1").And.Contain("stroke-dasharray").And.Contain("-.->");
    }
    [Test]
    public void Documentation_is_structured_and_html_is_encoded()
    {
        var d = TestDefinitions.Flow() with { Name = "<script>alert(1)</script>", Reliability = new("exponential", true, true), Monitoring = new(true, true) };
        var doc = new IntegrationDocumentationGenerator(new MermaidDiagramGenerator()).Generate(d, new IntegrationGraphBuilder().Build(d));
        doc.Sections.Select(s => s.Title).Should().Contain(["Overview", "Interfaces", "Reliability", "Security", "Logging", "Runbooks"]);
        new HtmlDocumentationRenderer().Render(doc).Should().Contain("&lt;script&gt;").And.NotContain("<script>");
        new MarkdownDocumentationRenderer().Render(doc).Should().Contain("```mermaid");
    }
    [Test]
    public async Task Global_graph_merges_only_explicit_shared_identities()
    {
        var first = TestDefinitions.Flow("first"); var second = TestDefinitions.Flow("second");
        first = first with { Nodes = [first.Nodes[0] with { SystemId = "shared" }, first.Nodes[1]] };
        second = second with { Nodes = [second.Nodes[0] with { SystemId = "shared" }, second.Nodes[1]] };
        var repository = new Mock<IIntegrationRepository>(); repository.Setup(r => r.GetDefinitionsAsync(Ct)).ReturnsAsync([first, second]);
        var graph = await new GlobalIntegrationGraphService(repository.Object).BuildAsync(Ct);
        graph.Nodes.Should().ContainSingle(n => n.Id == "system:shared");
        graph.Nodes.Count(n => n.Name == "B").Should().Be(2);
        graph.Nodes.Single(n => n.Id == "system:shared").IntegrationIds.Should().BeEquivalentTo(["first", "second"]);
    }
    [TestCase(false)]
    [TestCase(true)]
    public async Task Impact_analysis_traverses_chains_and_cycles(bool cyclic)
    {
        var d = TestDefinitions.Flow("flow", ("a", "b"), ("b", "c"), ("c", "d"), ("d", "e"));
        if (cyclic) d = d with { Edges = d.Edges.Append(new() { Id = "cycle", FromNodeId = "e", ToNodeId = "a" }).ToArray() };
        var repository = new Mock<IIntegrationRepository>(); repository.Setup(r => r.GetDefinitionsAsync(Ct)).ReturnsAsync([d]);
        var service = new ImpactAnalysisService(new GlobalIntegrationGraphService(repository.Object));
        var result = await service.AnalyzeAsync("node:flow:a", Ct);
        result.Downstream.Select(n => n.Id).Should().BeEquivalentTo(["node:flow:b", "node:flow:c", "node:flow:d", "node:flow:e"]);
        result.AffectedIntegrations.Should().Equal("flow");
        (await service.GetUpstreamDependencies("node:flow:c", Ct)).Select(n => n.Id).Should().Contain(["node:flow:a", "node:flow:b"]);
    }
    [Test]
    public async Task Impact_expands_declared_dependencies_without_inventing_flow_edges()
    {
        var first = TestDefinitions.Flow("first"); var second = TestDefinitions.Flow("second") with { Dependencies = [new("first")] };
        var repository = new Mock<IIntegrationRepository>(); repository.Setup(r => r.GetDefinitionsAsync(Ct)).ReturnsAsync([first, second]);
        var service = new ImpactAnalysisService(new GlobalIntegrationGraphService(repository.Object));
        var result = await service.AnalyzeAsync("node:first:a", Ct);
        result.AffectedIntegrations.Should().BeEquivalentTo(["first", "second"]);
        result.Downstream.Should().NotContain(n => n.Id.StartsWith("node:second:"));
        (await service.GetDependencies("second", Ct)).Should().Equal("first");
    }
    [Test]
    public async Task Message_analysis_finds_consumers()
    {
        var d = TestDefinitions.Flow() with { Messages = [new() { Name = "Created", Producer = "a", Consumers = ["b"] }] };
        var repository = new Mock<IIntegrationRepository>(); repository.Setup(r => r.GetDefinitionsAsync(Ct)).ReturnsAsync([d]);
        var service = new ImpactAnalysisService(new GlobalIntegrationGraphService(repository.Object));
        (await service.GetDownstreamDependencies("message:Created", Ct)).Select(n => n.Id).Should().Contain("node:test-flow:b");
    }
    [Test]
    public async Task Invalid_import_never_calls_persistence()
    {
        var repository = new Mock<IIntegrationRepository>();
        var parser = new Mock<IIntegrationDefinitionParser>(); parser.SetupGet(p => p.Format).Returns("yaml"); parser.Setup(p => p.Parse(It.IsAny<string>())).Returns(new ParseResult(null, "yaml", new([new("TEST", ValidationSeverity.Error, "Invalid")] )));
        var workflow = Workflow(parser.Object, repository.Object);
        var commands = new IntegrationCommands(workflow, repository.Object, NullLogger<IntegrationCommands>.Instance);
        await FluentActions.Awaiting(() => commands.Handle(new CreateIntegrationCommand(new("invalid"), "tester"), Ct)).Should().ThrowAsync<DefinitionValidationException>();
        repository.Verify(r => r.SaveAsync(It.IsAny<IntegrationDefinition>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ValidationResult>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool>(), Ct), Times.Never);
    }
    [Test]
    public async Task Missing_expected_revision_prevents_update()
    {
        var parser = new Mock<IIntegrationDefinitionParser>(); parser.SetupGet(p => p.Format).Returns("yaml"); parser.Setup(p => p.Parse(It.IsAny<string>())).Returns(new ParseResult(TestDefinitions.Flow(), "yaml", ValidationResult.Success));
        var commands = new IntegrationCommands(Workflow(parser.Object, Mock.Of<IIntegrationRepository>()), Mock.Of<IIntegrationRepository>(), NullLogger<IntegrationCommands>.Instance);
        await FluentActions.Awaiting(() => commands.Handle(new UpdateIntegrationCommand("test-flow", new("valid"), "tester"), Ct)).Should().ThrowAsync<ConflictException>();
    }
    [Test]
    public async Task Queries_return_not_found_for_missing_integration()
    {
        var queries = new IntegrationQueries(Mock.Of<IIntegrationRepository>(), new IntegrationGraphBuilder(), new MermaidDiagramGenerator(), new IntegrationDocumentationGenerator(new MermaidDiagramGenerator()));
        await FluentActions.Awaiting(() => queries.GetAsync("missing", Ct)).Should().ThrowAsync<NotFoundException>();
    }
    private static DefinitionWorkflow Workflow(IIntegrationDefinitionParser parser, IIntegrationRepository repository) => new([parser], new IntegrationGraphBuilder(), new(new IntegrationGraphBuilder()), new MermaidDiagramGenerator(), new IntegrationDocumentationGenerator(new MermaidDiagramGenerator()), repository, Mock.Of<ISystemRepository>(), NullLogger<DefinitionWorkflow>.Instance);
}
