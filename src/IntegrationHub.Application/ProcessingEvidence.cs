using IntegrationHub.Contracts;
using IntegrationHub.Domain;
namespace IntegrationHub.Application;

public static class ProcessingEvidence
{
    public sealed record Step(ProcessingReference Reference, string? Input, IReadOnlyList<string> Outputs, IReadOnlyList<string> Calls);
    public static IEnumerable<Step> Steps(ComponentProcessing? processing)
    {
        if (processing is null) yield break;
        foreach (var handler in processing.Handlers.OrderBy(h => h.Id, StringComparer.Ordinal))
            foreach (var rule in handler.Handles.OrderBy(h => h.Message, StringComparer.Ordinal))
                yield return new(new(handler.Id, Name(handler.Name, handler.Id), "Handler", rule.Message, false, false, rule.Condition), rule.Message, rule.Outputs, rule.Calls);
        foreach (var saga in processing.Sagas.OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            foreach (var rule in saga.Handles.OrderBy(h => h.Message, StringComparer.Ordinal))
                yield return new(new(saga.Id, Name(saga.Name, saga.Id), "Saga", rule.Message, rule.StartsSaga, rule.CompletesSaga, rule.Condition), rule.Message, rule.Outputs, rule.Calls);
            foreach (var timeout in saga.Timeouts.OrderBy(t => t.Id, StringComparer.Ordinal))
                yield return new(new(saga.Id, Name(saga.Name, saga.Id), "Saga timeout", timeout.Id, false, timeout.CompletesSaga, timeout.Condition), null, timeout.Outputs, timeout.Calls);
        }
    }
    private static string Name(string name, string id) => string.IsNullOrWhiteSpace(name) ? id : name;
    public static IEnumerable<string> SearchTerms(ComponentProcessing? p)
    {
        if (p is null) return [];
        return new[] { p.Framework, p.Endpoint, p.Transport, p.Persistence, p.Recoverability }
            .Concat(p.Handlers.SelectMany(h => new[] { h.Id, h.Name, h.Implementation, h.Description }))
            .Concat(p.Sagas.SelectMany(s => new[] { s.Id, s.Name, s.Implementation, s.Description, s.DataType, s.CorrelationProperty ?? "" }));
    }
    public static IEnumerable<string> Documentation(DiscoveredComponentProcessing item)
    {
        var p = item.Definition;
        yield return $"{item.ComponentName} ({item.ComponentId}) · {p.Framework} endpoint {p.Endpoint}; transport: {p.Transport}; persistence: {p.Persistence}; send-only: {p.SendOnly}; Outbox: {p.Outbox?.ToString() ?? "not documented"}.";
        if (p.Recoverability.Length > 0) yield return $"Recoverability notes: {p.Recoverability}";
        foreach (var step in Steps(p))
            yield return $"{step.Reference.Kind} {step.Reference.Name} ({step.Reference.ProcessorId}) · trigger {step.Reference.Trigger}; starts or continues saga: {step.Reference.StartsSaga}; may complete: {step.Reference.CompletesSaga}; possible outputs: {string.Join(", ", step.Outputs)}; possible calls: {string.Join(", ", step.Calls)}; condition: {step.Reference.Condition}";
        foreach (var saga in p.Sagas)
        {
            yield return $"Saga {saga.Id}: data {saga.DataType}; correlation property {saga.CorrelationProperty}; not found: {saga.WhenNotFound}. {saga.NotFoundDescription}";
            foreach (var rule in saga.Handles)
                yield return $"{saga.Id}/{rule.Message}: correlation {rule.Correlation?.Mode} {rule.Correlation?.MessageProperty ?? rule.Correlation?.Header}; {rule.Correlation?.Description}; requests timeouts: {string.Join(", ", rule.RequestsTimeouts)}";
            foreach (var timeout in saga.Timeouts)
                yield return $"{saga.Id}/timeout:{timeout.Id}: state type {timeout.StateType}; schedule {timeout.Delay ?? timeout.Schedule ?? "not documented"}; requests timeouts: {string.Join(", ", timeout.RequestsTimeouts)}";
        }
    }
}
