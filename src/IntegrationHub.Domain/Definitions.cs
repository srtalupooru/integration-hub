namespace IntegrationHub.Domain;

public readonly record struct IntegrationId
{
    public string Value { get; }
    public IntegrationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 ||
            !System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-z0-9][a-z0-9._-]*$"))
            throw new ArgumentException("Integration ID must be a lowercase slug of at most 128 characters.", nameof(value));
        Value = value;
    }
    public override string ToString() => Value;
}

public enum IntegrationStatus { Draft, Development, Test, Production, Deprecated, Retired }
public enum Criticality { Low, Medium, High, Critical }
public enum NodeType { ExternalSystem, InternalSystem, Api, ApiManagement, ServiceBusTopic, ServiceBusQueue, AzureFunction, LogicApp, Database, Storage, EventGrid, Library, NuGetPackage, Sftp, FileShare, SaaS, MessageBroker, Transformation, ManualProcess, Custom }
public enum MessageType { Event, Command, Document, Request, Response }
public enum InteractionMode { Synchronous, Asynchronous }
public enum EdgeDirection { Forward, Bidirectional }

public sealed record IntegrationDefinition
{
    public IntegrationId Id { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Version { get; init; } = "1.0";
    public IntegrationStatus Status { get; init; }
    public string BusinessDomain { get; init; } = "";
    public Ownership Ownership { get; init; } = new();
    public Criticality Criticality { get; init; }
    public string Environment { get; init; } = "";
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> Sources { get; init; } = [];
    public IReadOnlyList<string> Destinations { get; init; } = [];
    public IReadOnlyList<IntegrationNode> Nodes { get; init; } = [];
    public IReadOnlyList<IntegrationEdge> Edges { get; init; } = [];
    public IReadOnlyList<LogicalGroup> Groups { get; init; } = [];
    public IReadOnlyList<IntegrationMessage> Messages { get; init; } = [];
    public IReadOnlyList<IntegrationMessage> Events { get; init; } = [];
    public IReadOnlyList<IntegrationDependency> Dependencies { get; init; } = [];
    public MonitoringDefinition Monitoring { get; init; } = new();
    public ReliabilityDefinition Reliability { get; init; } = new();
    public SecurityDefinition Security { get; init; } = new();
    public IReadOnlyList<NamedLink> Repositories { get; init; } = [];
    public IReadOnlyList<NamedLink> Runbooks { get; init; } = [];
    public IReadOnlyList<Contact> Contacts { get; init; } = [];
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
    public IEnumerable<IntegrationMessage> AllMessages => Messages.Concat(Events);
}

public sealed record Ownership(string Team = "", string SupportTeam = "");
public sealed record NamedLink(string Name, string Url);
public sealed record Contact(string Name, string Role, string Email);
public sealed record LogicalGroup(string Id, string Name, string? ParentId = null);
public sealed record IntegrationDependency(string IntegrationId, string Description = "");
public sealed record MonitoringDefinition(bool ApplicationInsights = false, bool AlertsEnabled = false, string Logging = "", string Alerting = "", string DashboardUrl = "", string Notes = "");
public sealed record ReliabilityDefinition(string Retries = "", bool DeadLetterQueue = false, bool Idempotency = false, string ErrorHandling = "", string DeadLetterHandling = "", string Notes = "");
public sealed record SecurityDefinition(string Authentication = "", string DataClassification = "", string Encryption = "", string Notes = "");
public sealed record RuntimeReference(string Kind, string ResourceId);
public sealed record IntegrationNode
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public NodeType Type { get; init; }
    public string? CustomType { get; init; }
    public string Description { get; init; } = "";
    public string Technology { get; init; } = "";
    public string Owner { get; init; } = "";
    public string Environment { get; init; } = "";
    public string? SystemId { get; init; }
    // Explicit stable identity for shared infrastructure; names are never used to merge nodes.
    public string? SharedResourceId { get; init; }
    public string? GroupId { get; init; }
    public bool Optional { get; init; }
    public string? RepositoryUrl { get; init; }
    public string? DocumentationUrl { get; init; }
    public IReadOnlyList<RuntimeReference> RuntimeReferences { get; init; } = [];
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
public sealed record IntegrationEdge
{
    public string Id { get; init; } = "";
    public string FromNodeId { get; init; } = "";
    public string ToNodeId { get; init; } = "";
    public string Label { get; init; } = "";
    public string Description { get; init; } = "";
    public string Protocol { get; init; } = "";
    public string? MessageName { get; init; }
    public string TransportType { get; init; } = "";
    public EdgeDirection Direction { get; init; }
    public InteractionMode Mode { get; init; }
    public string AuthenticationType { get; init; } = "";
    public string RetryBehaviour { get; init; } = "";
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
public sealed record IntegrationMessage
{
    public string Name { get; init; } = "";
    public MessageType Type { get; init; } = MessageType.Event;
    public string Description { get; init; } = "";
    public string Schema { get; init; } = "";
    public string Producer { get; init; } = "";
    public IReadOnlyList<string> Consumers { get; init; } = [];
    public string Version { get; init; } = "1.0";
    public string TopicOrQueue { get; init; } = "";
    public string ContentType { get; init; } = "";
    public string ExamplePayload { get; init; } = "";
    public string Documentation { get; init; } = "";
}
public sealed record SystemDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Type { get; init; } = "";
    public string Owner { get; init; } = "";
    public string BusinessDomain { get; init; } = "";
    public Criticality Criticality { get; init; }
    public string Vendor { get; init; } = "";
    public string SupportContact { get; init; } = "";
    public string? DocumentationUrl { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
