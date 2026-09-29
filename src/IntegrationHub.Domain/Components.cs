namespace IntegrationHub.Domain;

// A component ID identifies one deployed/documented component in one environment.
public enum ChannelKind { Topic, Queue, Http, Stream }
public enum HttpVerb { GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS, TRACE, CONNECT }
public enum ComponentInteractionKind { Message, HttpCall }
public sealed record ApiEndpointDefinition
{
    public string Id { get; init; } = "";
    public HttpVerb Method { get; init; }
    public string Path { get; init; } = "";
    public string Version { get; init; } = "1.0";
    public string Description { get; init; } = "";
}
public sealed record ApiCallDefinition
{
    public string Id { get; init; } = "";
    public string TargetComponent { get; init; } = "";
    public string Endpoint { get; init; } = "";
    public HttpVerb Method { get; init; }
    public string Version { get; init; } = "1.0";
    public string Description { get; init; } = "";
}
public sealed record MessageChannel
{
    public ChannelKind Kind { get; init; }
    public string Namespace { get; init; } = "";
    public string Name { get; init; } = "";
}
public sealed record MessageBinding
{
    public string Id { get; init; } = "";
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Action { get; init; }
    public string Contract { get; init; } = "";
    public string Version { get; init; } = "";
    public MessageType MessageType { get; init; } = MessageType.Event;
    public MessageChannel Channel { get; init; } = new();
    public string ContentType { get; init; } = "application/json";
    public string? SchemaFingerprint { get; init; }
    public string Description { get; init; } = "";
    // Optional explicit disambiguation, never inferred from display names.
    public string? SourceComponent { get; init; }
    public string? TargetComponent { get; init; }
    // Topic subscription or stream consumer group. Shared groups compete for delivery.
    public string? Subscription { get; init; }
}
public sealed record ComponentDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "1.0";
    public NodeType Type { get; init; }
    public string Environment { get; init; } = "";
    public string Domain { get; init; } = "";
    public string Description { get; init; } = "";
    public string Owner { get; init; } = "";
    [System.Text.Json.Serialization.JsonConverter(typeof(TechnologyListJsonConverter))]
    public IReadOnlyList<string> Technology { get; init; } = [];
    public IntegrationStatus Status { get; init; } = IntegrationStatus.Draft;
    public Criticality Criticality { get; init; } = Criticality.Medium;
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<MessageBinding> Messages { get; init; } = [];
    // Legacy source fields remain readable. Derived views are never persisted twice.
    public IReadOnlyList<MessageBinding> Consumes { get; init; } = [];
    public IReadOnlyList<MessageBinding> Publishes { get; init; } = [];
    public IReadOnlyList<MessageBinding> Sends { get; init; } = [];
    public IReadOnlyList<ApiEndpointDefinition> Endpoints { get; init; } = [];
    public IReadOnlyList<ApiCallDefinition> Calls { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ComponentProcessing? Processing { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<MessageBinding> ConsumedMessages => Consumes.Concat(Messages.Where(m => m.Action == "consumes")).ToArray();
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<MessageBinding> PublishedMessages => Publishes.Concat(Messages.Where(m => m.Action == "publishes")).ToArray();
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<MessageBinding> SentMessages => Sends.Concat(Messages.Where(m => m.Action == "sends")).ToArray();
}
