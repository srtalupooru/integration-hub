using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using IntegrationHub.Contracts;
using IntegrationHub.Domain;
namespace IntegrationHub.Application;

public sealed class MermaidDiagramGenerator : IDiagramGenerator
{
    // Definitions never supply Mermaid syntax or identifiers.
    private static string Escape(string value) => string.Concat(value.EnumerateRunes().Select(r => r.IsAscii && (char.IsAsciiLetterOrDigit((char)r.Value) || r.Value == ' ') ? r.ToString() : $"#{r.Value};"));
    private static string CardLabel(string name)
    {
        // Break long labels before layout so card bounds and attached edges agree.
        var lines = new List<string>();
        var line = new StringBuilder();
        foreach (var word in name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > 28)
            {
                lines.Add(Escape(line.ToString()));
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            foreach (var rune in word.EnumerateRunes())
            {
                if (line.Length >= 28) { lines.Add(Escape(line.ToString())); line.Clear(); }
                line.Append(rune.ToString());
            }
        }
        if (line.Length > 0) lines.Add(Escape(line.ToString()));
        return string.Join("<br/>", lines);
    }
    public string Generate(IntegrationGraph graph)
    {
        var text = new StringBuilder("flowchart LR\n");
        text.AppendLine("  classDef system fill:#eaf2ff,stroke:#577fb8,color:#1b2e3d");
        text.AppendLine("  classDef api fill:#e3f3ee,stroke:#388c76,color:#1b2e3d");
        text.AppendLine("  classDef messaging fill:#fff3d6,stroke:#b88b30,color:#1b2e3d");
        text.AppendLine("  classDef compute fill:#f0eafa,stroke:#8a6bb0,color:#1b2e3d");
        text.AppendLine("  classDef data fill:#e8eff3,stroke:#607e91,color:#1b2e3d");
        text.AppendLine("  classDef other fill:#f3f4f6,stroke:#86909c,color:#1b2e3d");
        var ids = graph.Nodes.Select((n, i) => (n.Id, Safe: $"n{i}")).ToDictionary(x => x.Id, x => x.Safe);
        var groups = graph.Groups.Select((g, i) => (g.Id, Safe: $"g{i}")).ToDictionary(x => x.Id, x => x.Safe);
        var inputs = graph.Edges.Select(e => e.ToNodeId).Concat(graph.Edges.Where(e => e.Direction == EdgeDirection.Bidirectional).Select(e => e.FromNodeId)).ToHashSet();
        var outputs = graph.Edges.Select(e => e.FromNodeId).Concat(graph.Edges.Where(e => e.Direction == EdgeDirection.Bidirectional).Select(e => e.ToNodeId)).ToHashSet();
        void Node(IntegrationNode node)
        {
            text.AppendLine($"  {ids[node.Id]}[\"{CardLabel(node.Name)}\"]");
            var category = node.Type switch
            {
                NodeType.ExternalSystem or NodeType.InternalSystem or NodeType.SaaS => "system",
                NodeType.Api or NodeType.ApiManagement => "api",
                NodeType.ServiceBusTopic or NodeType.ServiceBusQueue or NodeType.MessageBroker or NodeType.EventGrid => "messaging",
                NodeType.AzureFunction or NodeType.LogicApp or NodeType.Library or NodeType.NuGetPackage or NodeType.Transformation => "compute",
                NodeType.Database or NodeType.Storage or NodeType.Sftp or NodeType.FileShare => "data",
                _ => "other"
            };
            text.AppendLine($"  class {ids[node.Id]} {category}");
            // Only enum-derived classes reach the SVG decorator; authored values cannot supply CSS.
            text.AppendLine($"  class {ids[node.Id]} hub-type-{node.Type}");
            if (inputs.Contains(node.Id))
                text.AppendLine($"  class {ids[node.Id]} hub-input");
            if (outputs.Contains(node.Id))
                text.AppendLine($"  class {ids[node.Id]} hub-output");
            if (node.Optional) text.AppendLine($"  style {ids[node.Id]} stroke-dasharray: 5 5");
        }
        void Group(LogicalGroup group)
        {
            text.AppendLine($"  subgraph {groups[group.Id]}[\"{Escape(group.Name)}\"]");
            foreach (var node in graph.Nodes.Where(n => n.GroupId == group.Id)) Node(node);
            foreach (var child in graph.Groups.Where(g => g.ParentId == group.Id)) Group(child);
            text.AppendLine("  end");
        }
        foreach (var node in graph.Nodes.Where(n => n.GroupId is null)) Node(node);
        foreach (var group in graph.Groups.Where(g => g.ParentId is null)) Group(group);
        foreach (var edge in graph.Edges)
        {
            var arrow = edge.Direction == EdgeDirection.Bidirectional ? "<-->" : edge.Mode == InteractionMode.Asynchronous ? "-.->" : "-->";
            var label = string.IsNullOrEmpty(edge.Label) ? "" : $"|\"{Escape(edge.Label)}\"|";
            text.AppendLine($"  {ids[edge.FromNodeId]} {arrow}{label} {ids[edge.ToNodeId]}");
        }
        return text.ToString();
    }
}

public sealed class IntegrationDocumentationGenerator(IDiagramGenerator diagrams, ILogger<IntegrationDocumentationGenerator>? logger = null) : IIntegrationDocumentationGenerator
{
    public IntegrationDocumentation Generate(IntegrationDefinition d, IntegrationGraph graph)
    {
        logger?.LogDebug("Generating documentation for {IntegrationId}", d.Id.Value);
        var sections = new List<DocumentationSection>();
        void Add(string title, IEnumerable<string> lines) => sections.Add(new(title, lines.Where(l => !string.IsNullOrWhiteSpace(l)).DefaultIfEmpty("Not documented.").ToArray()));
        string NodeName(string id) => d.Nodes.FirstOrDefault(n => n.Id == id)?.Name ?? id;
        Add("Overview", [$"Status: {d.Status}", $"Version: {d.Version}", $"Business domain: {d.BusinessDomain}", $"Criticality: {d.Criticality}", $"Environment: {d.Environment}", $"Owner: {d.Ownership.Team}", $"Support team: {d.Ownership.SupportTeam}"]);
        Add("Source systems", graph.EntryNodes.Select(NodeName));
        Add("Destination systems", graph.ExitNodes.Select(NodeName));
        Add("End-to-end flow", graph.Edges.Select(e => $"{NodeName(e.FromNodeId)} → {NodeName(e.ToNodeId)}: {e.Label} ({e.Mode}, {e.Direction})"));
        Add("Interfaces", d.Edges.Select(e => $"{e.Id}: {e.Protocol} / {e.TransportType}; authentication: {e.AuthenticationType}; retry: {e.RetryBehaviour}. {e.Description}"));
        Add("Systems", d.Nodes.Select(n => $"{n.Name} [{n.Type}]: {n.Description}; technology: {n.Technology}; owner: {n.Owner}"));
        Add("APIs", d.Nodes.Where(n => n.Type is NodeType.Api or NodeType.ApiManagement).Select(n => $"{n.Name}: {n.Description} {n.DocumentationUrl}"));
        foreach (var kind in new[] { "Messages", "Events" })
            Add(kind, d.AllMessages.Where(m => (m.Type == MessageType.Event) == (kind == "Events")).Select(m => $"{m.Name} v{m.Version}: {NodeName(m.Producer)} → {string.Join(", ", m.Consumers.Select(NodeName))}. {m.Description}; content type: {m.ContentType}; topic/queue: {m.TopicOrQueue}; schema: {m.Schema}; example: {m.ExamplePayload}; documentation: {m.Documentation}"));
        Add("Dependencies", d.Dependencies.Select(dep => $"{dep.IntegrationId}: {dep.Description}"));
        Add("Reliability", [$"Retries: {d.Reliability.Retries}", $"Idempotency: {d.Reliability.Idempotency}", d.Reliability.Notes]);
        Add("Error handling", [d.Reliability.ErrorHandling]);
        Add("Dead-letter handling", [$"Enabled: {d.Reliability.DeadLetterQueue}", d.Reliability.DeadLetterHandling]);
        Add("Security", [$"Authentication: {d.Security.Authentication}", $"Data classification: {d.Security.DataClassification}", $"Encryption: {d.Security.Encryption}", d.Security.Notes]);
        Add("Monitoring", [$"Application Insights configured: {d.Monitoring.ApplicationInsights}", d.Monitoring.DashboardUrl, d.Monitoring.Notes]);
        Add("Logging", [d.Monitoring.Logging]);
        Add("Alerting", [$"Enabled: {d.Monitoring.AlertsEnabled}", d.Monitoring.Alerting]);
        Add("Operational support", [d.Ownership.SupportTeam]);
        Add("Runbooks", d.Runbooks.Select(r => $"{r.Name}: {r.Url}"));
        Add("Repositories", d.Repositories.Select(r => $"{r.Name}: {r.Url}").Concat(d.Nodes.Where(n => n.RepositoryUrl is not null).Select(n => $"{n.Name}: {n.RepositoryUrl}")));
        Add("Contacts", d.Contacts.Select(c => $"{c.Name} ({c.Role}): {c.Email}"));
        Add("Metadata", d.Metadata.Select(m => $"{m.Key}: {m.Value}").Concat(d.Tags.Select(t => $"Tag: {t}")));
        return new(d.Id.Value, d.Name, d.Description, diagrams.Generate(graph), sections);
    }
}
public sealed class MarkdownDocumentationRenderer : IDocumentationRenderer
{
    public string Format => "markdown";
    private static string Escape(string value) => WebUtility.HtmlEncode(value).Replace("\\", "\\\\").Replace("`", "\\`").Replace("*", "\\*").Replace("[", "\\[").Replace("]", "\\]").Replace("_", "\\_").Replace("#", "\\#").Replace("\r", "").Replace("\n", " ");
    public string Render(IntegrationDocumentation documentation)
    {
        var text = new StringBuilder($"# {Escape(documentation.Name)}\n\n{Escape(documentation.Description)}\n\n## Architecture\n\n```mermaid\n{documentation.Diagram}```\n");
        foreach (var section in documentation.Sections)
        {
            text.AppendLine($"\n## {section.Title}\n");
            foreach (var line in section.Lines) text.AppendLine($"- {Escape(line)}");
        }
        return text.ToString();
    }
}
public sealed class HtmlDocumentationRenderer : IDocumentationRenderer
{
    public string Format => "html";
    public string Render(IntegrationDocumentation documentation)
    {
        var text = new StringBuilder($"<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>{WebUtility.HtmlEncode(documentation.Name)}</title><body><main><h1>{WebUtility.HtmlEncode(documentation.Name)}</h1><p>{WebUtility.HtmlEncode(documentation.Description)}</p><h2>Architecture (Mermaid)</h2><pre>{WebUtility.HtmlEncode(documentation.Diagram)}</pre>");
        foreach (var section in documentation.Sections)
        {
            text.Append($"<section><h2>{WebUtility.HtmlEncode(section.Title)}</h2><ul>");
            foreach (var line in section.Lines) text.Append($"<li>{WebUtility.HtmlEncode(line)}</li>");
            text.Append("</ul></section>");
        }
        return text.Append("</main></body></html>").ToString();
    }
}
