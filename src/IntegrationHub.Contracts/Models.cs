using IntegrationHub.Domain;
namespace IntegrationHub.Contracts;

public sealed record DefinitionRequest(string Definition, string? Format = null, string ChangeSummary = "", int? ExpectedRevision = null);
public sealed record ParseResult(IntegrationDefinition? Definition, string Format, ValidationResult Validation);
public sealed record PreviewResult(IntegrationDefinition? Definition, ValidationResult Validation, string? Diagram, IntegrationDocumentation? Documentation, string Format);
public sealed record DocumentationSection(string Title, IReadOnlyList<string> Lines);
public sealed record IntegrationDocumentation(string Id, string Name, string Description, string Diagram, IReadOnlyList<DocumentationSection> Sections);
public sealed record IntegrationSummary(string Id, string Name, string Description, string Version, string Status, string BusinessDomain, string Owner, string Criticality, string Environment, IReadOnlyList<string> Tags, int Revision, DateTimeOffset UpdatedAt, int WarningCount);
public sealed record CatalogueEntry(IntegrationSummary Summary, string Origin)
{
    public string DetailUrl => $"/{(Origin == "Discovered" ? "discovered" : "integrations")}/{Uri.EscapeDataString(Summary.Id)}";
}
public sealed record IntegrationDetail(IntegrationDefinition Definition, string OriginalDefinition, string Format, int Revision, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string CreatedBy, string DefinitionHash, ValidationResult Validation);
public sealed record DefinitionVersion(int Revision, string Version, DateTimeOffset Timestamp, string ChangedBy, string ChangeSummary, string DefinitionHash, string Format, string OriginalDefinition);
public sealed record SearchRequest(string? Q = null, string? Status = null, string? Domain = null, string? Owner = null, string? Criticality = null, string? Tag = null, string? System = null, string? Technology = null, string? Source = null, string? Destination = null, int Page = 1, int PageSize = 24);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record CountBy(string Name, int Count);
public sealed record DashboardSummary(int TotalIntegrations, int ProductionIntegrations, int Systems, int Messages, int HighCriticalityIntegrations, int ValidationWarnings, IReadOnlyList<IntegrationSummary> RecentlyUpdated, IReadOnlyList<CountBy> ByDomain, IReadOnlyList<CountBy> ByStatus, IReadOnlyList<CountBy> MostReferencedSystems);
public sealed record MessageOccurrence(string IntegrationId, string IntegrationName, IntegrationMessage Message);
public sealed record MessageCatalogueEntry(string Name, IReadOnlyList<MessageOccurrence> Occurrences);
public sealed record GlobalNode(string Id, string Name, string Kind, IReadOnlyList<string> IntegrationIds);
public sealed record GlobalEdge(string From, string To, string Kind, string IntegrationId, InteractionMode Mode = InteractionMode.Synchronous);
public sealed record GlobalGraph(IReadOnlyList<GlobalNode> Nodes, IReadOnlyList<GlobalEdge> Edges);
public sealed record ImpactResult(string ComponentId, IReadOnlyList<GlobalNode> Upstream, IReadOnlyList<GlobalNode> Downstream, IReadOnlyList<string> DirectIntegrations, IReadOnlyList<string> AffectedIntegrations, IReadOnlyList<GlobalNode> DependentSystems);
public sealed record SessionInfo(string Name, IReadOnlyList<string> Roles, string Environment, string CsrfToken);
