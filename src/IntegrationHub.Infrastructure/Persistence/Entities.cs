namespace IntegrationHub.Infrastructure.Persistence;

public sealed class IntegrationEntity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public string Status { get; set; } = "";
    public string BusinessDomain { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Criticality { get; set; } = "";
    public string Environment { get; set; } = "";
    public string CanonicalJson { get; set; } = "";
    public string OriginalDefinition { get; set; } = "";
    public string Format { get; set; } = "";
    public string DefinitionHash { get; set; } = "";
    public string ValidationJson { get; set; } = "";
    public int WarningCount { get; set; }
    public int Revision { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public List<IntegrationNodeEntity> Nodes { get; set; } = [];
    public List<IntegrationEdgeEntity> Edges { get; set; } = [];
    public List<IntegrationMessageEntity> Messages { get; set; } = [];
    public List<TagEntity> Tags { get; set; } = [];
    public List<RepositoryEntity> Repositories { get; set; } = [];
    public List<RunbookEntity> Runbooks { get; set; } = [];
    public List<DependencyEntity> Dependencies { get; set; } = [];
}
public sealed class IntegrationNodeEntity
{
    public string IntegrationId { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Technology { get; set; } = "";
    public string Owner { get; set; } = "";
    public string? SystemId { get; set; }
    public string? SharedResourceId { get; set; }
    public bool IsSource { get; set; }
    public bool IsDestination { get; set; }
    public string DefinitionJson { get; set; } = "";
}
public sealed class IntegrationEdgeEntity
{
    public string IntegrationId { get; set; } = "";
    public string Id { get; set; } = "";
    public string FromNodeId { get; set; } = "";
    public string ToNodeId { get; set; } = "";
    public string Label { get; set; } = "";
    public string Protocol { get; set; } = "";
    public string? MessageName { get; set; }
    public string DefinitionJson { get; set; } = "";
}
public sealed class SystemEntity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Owner { get; set; } = "";
    public string BusinessDomain { get; set; } = "";
    public string DefinitionJson { get; set; } = "";
}
public sealed class MessageEntity
{
    public string Name { get; set; } = "";
}
public sealed class IntegrationMessageEntity
{
    public string IntegrationId { get; set; } = "";
    public string MessageName { get; set; } = "";
    public string Producer { get; set; } = "";
    public string Type { get; set; } = "";
    public string TopicOrQueue { get; set; } = "";
    public string Version { get; set; } = "";
    public string DefinitionJson { get; set; } = "";
}
public sealed class RepositoryEntity
{
    public long Id { get; set; }
    public string IntegrationId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}
public sealed class RunbookEntity
{
    public long Id { get; set; }
    public string IntegrationId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}
public sealed class TagEntity
{
    public string IntegrationId { get; set; } = "";
    public string Value { get; set; } = "";
}
public sealed class DependencyEntity
{
    public string IntegrationId { get; set; } = "";
    public string TargetIntegrationId { get; set; } = "";
    public string Description { get; set; } = "";
}
public sealed class DefinitionVersionEntity
{
    public string IntegrationId { get; set; } = "";
    public int Revision { get; set; }
    public string Version { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; }
    public string ChangedBy { get; set; } = "";
    public string ChangeSummary { get; set; } = "";
    public string DefinitionHash { get; set; } = "";
    public string Format { get; set; } = "";
    public string OriginalDefinition { get; set; } = "";
    public string CanonicalJson { get; set; } = "";
}

public sealed class ComponentEntity
{
    public string Id { get; set; } = "";
    public string CanonicalJson { get; set; } = "";
    public string OriginalDefinition { get; set; } = "";
    public string Format { get; set; } = "";
    public string DefinitionHash { get; set; } = "";
    public int Revision { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string ChangedBy { get; set; } = "";
    public bool IsArchived { get; set; }
}
public sealed class ComponentVersionEntity
{
    public string ComponentId { get; set; } = "";
    public int Revision { get; set; }
    public string Version { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; }
    public string ChangedBy { get; set; } = "";
    public string ChangeSummary { get; set; } = "";
    public string DefinitionHash { get; set; } = "";
    public string Format { get; set; } = "";
    public string OriginalDefinition { get; set; } = "";
    public string CanonicalJson { get; set; } = "";
    public bool IsArchived { get; set; }
}
