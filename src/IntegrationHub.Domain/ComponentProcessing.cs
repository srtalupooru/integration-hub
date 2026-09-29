namespace IntegrationHub.Domain;

// Design-time declarations only. These do not execute handlers or store saga instances.
public sealed record ComponentProcessing
{
    public string Framework { get; init; } = "NServiceBus";
    public string Endpoint { get; init; } = "";
    public string Transport { get; init; } = "";
    public string Persistence { get; init; } = "";
    public bool SendOnly { get; init; }
    public bool? Outbox { get; init; }
    public string Recoverability { get; init; } = "";
    public IReadOnlyList<HandlerDefinition> Handlers { get; init; } = [];
    public IReadOnlyList<SagaDefinition> Sagas { get; init; } = [];
}
public record ProcessingEffects
{
    public IReadOnlyList<string> Outputs { get; init; } = [];
    public IReadOnlyList<string> Calls { get; init; } = [];
    public string Condition { get; init; } = "";
}
public record HandlingRule : ProcessingEffects
{
    public string Message { get; init; } = "";
}
public sealed record HandlerDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Implementation { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<HandlingRule> Handles { get; init; } = [];
}
public sealed record SagaCorrelation
{
    public string Mode { get; init; } = "Property";
    public string? MessageProperty { get; init; }
    public string? Header { get; init; }
    public string Description { get; init; } = "";
}
public sealed record SagaHandlingRule : HandlingRule
{
    public bool StartsSaga { get; init; }
    public SagaCorrelation? Correlation { get; init; }
    public IReadOnlyList<string> RequestsTimeouts { get; init; } = [];
    public bool CompletesSaga { get; init; }
}
public sealed record SagaTimeoutDefinition : ProcessingEffects
{
    public string Id { get; init; } = "";
    public string StateType { get; init; } = "";
    public string? Delay { get; init; }
    public string? Schedule { get; init; }
    public IReadOnlyList<string> RequestsTimeouts { get; init; } = [];
    public bool CompletesSaga { get; init; }
}
public sealed record SagaDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Implementation { get; init; } = "";
    public string Description { get; init; } = "";
    public string DataType { get; init; } = "";
    public string? CorrelationProperty { get; init; }
    public string WhenNotFound { get; init; } = "Discard";
    public string NotFoundDescription { get; init; } = "";
    public IReadOnlyList<SagaHandlingRule> Handles { get; init; } = [];
    public IReadOnlyList<SagaTimeoutDefinition> Timeouts { get; init; } = [];
}
