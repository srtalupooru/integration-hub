using IntegrationHub.Domain;
namespace IntegrationHub.Contracts;

public sealed record ComponentParseResult(ComponentDefinition? Definition, string Format, ValidationResult Validation);
public sealed record ComponentDetail(ComponentDefinition Definition, string OriginalDefinition, string Format, int Revision,
    string DefinitionHash, DateTimeOffset UpdatedAt, string ChangedBy, bool IsArchived);
public sealed record ComponentSummary(string Id, string Name, string Type, string Environment, string Owner, string Status,
    int Consumes, int Publishes, int Revision, DateTimeOffset UpdatedAt, bool IsArchived, int Endpoints = 0, int Calls = 0, int Sends = 0);
public sealed record ComponentVersion(int Revision, string Version, DateTimeOffset Timestamp, string ChangedBy,
    string ChangeSummary, string DefinitionHash, string Format, string OriginalDefinition, bool IsArchived);
public sealed record ComponentSource(string Id, string Name, int Revision, string DefinitionHash);
public sealed record DiscoveryIssue(string Code, string ComponentId, string? BindingId, string Message, string Resolution);
public sealed record DiscoveredConnection(string Id, string ProducerId, string PublishBindingId, string ConsumerId,
    string ConsumeBindingId, string Contract, string Version, MessageType MessageType, MessageChannel Channel,
    string Delivery, string? Subscription, ComponentInteractionKind Kind = ComponentInteractionKind.Message, string? HttpMethod = null, string? HttpPath = null)
{
    public IReadOnlyList<ProcessingReference> HandledBy { get; init; } = [];
    public IReadOnlyList<ProcessingReference> ProducedBy { get; init; } = [];
}
public sealed record ProcessingReference(string ProcessorId, string Name, string Kind, string Trigger,
    bool StartsSaga, bool CompletesSaga, string Condition);
public sealed record DiscoveredComponentProcessing(string ComponentId, string ComponentName, ComponentProcessing Definition);
public sealed record DiscoveredIntegration(string Id, string Name, string Environment, IReadOnlyList<ComponentSource> Sources,
    IntegrationDefinition Definition, IReadOnlyList<DiscoveredConnection> Connections, IReadOnlyList<DiscoveryIssue> Issues, bool HasCycle)
{
    public IReadOnlyList<DiscoveredComponentProcessing> Processing { get; init; } = [];
}
public sealed record DiscoveryResult(string Fingerprint, IReadOnlyList<DiscoveredIntegration> Integrations,
    IReadOnlyList<DiscoveryIssue> Issues, IReadOnlyList<string> UnlinkedComponentIds, IReadOnlyList<string> RetiredComponentIds);
public sealed record DiscoveredIntegrationView(string Fingerprint, DiscoveredIntegration Integration, string Diagram, IntegrationDocumentation Documentation);
public sealed record ComponentPreview(ComponentDefinition? Definition, string Format, ValidationResult Validation,
    DiscoveryResult? Discovery, IReadOnlyList<string> RemovedIntegrationIds, IReadOnlyList<string> AddedIntegrationIds);
public sealed record ComponentMessageOccurrence(string ComponentId, string ComponentName, string Environment, string Direction, MessageBinding Binding);
public sealed record ComponentDashboard(int Components, int Networks, int Unlinked, int Findings);
