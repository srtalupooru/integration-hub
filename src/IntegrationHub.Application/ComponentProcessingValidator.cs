using System.Xml;
using IntegrationHub.Domain;
namespace IntegrationHub.Application;

public static class ComponentProcessingValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(ComponentDefinition component)
    {
        var p = component.Processing;
        if (p is null) return [];
        var issues = new List<ValidationIssue>();
        void Issue(string code, string message, string? id = null, bool warning = false) => issues.Add(new(code,
            warning ? ValidationSeverity.Warning : ValidationSeverity.Error, message, NodeId: id, Path: "/component/processing"));
        if (p.Framework != "NServiceBus" || string.IsNullOrWhiteSpace(p.Endpoint))
            Issue("PROCESSING_ENDPOINT", "processing requires framework: NServiceBus and a logical endpoint name.");
        if (p.Handlers.Sum(h => h.Handles.Count) + p.Sagas.Sum(s => s.Handles.Count + s.Timeouts.Count) > 500)
        {
            Issue("PROCESSING_LIMIT", "A component can document at most 500 handling and timeout rules.");
            return issues;
        }
        var inputs = component.ConsumedMessages.Where(m => m.Channel.Kind != ChannelKind.Http).Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        var outputs = component.PublishedMessages.Concat(component.SentMessages).Where(m => m.Channel.Kind != ChannelKind.Http).Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        var calls = component.Calls.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var handled = new List<string>();
        void Duplicates(IEnumerable<string> values, string context)
        {
            foreach (var id in values.GroupBy(v => v, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key))
                Issue("PROCESSING_DUPLICATE", $"Duplicate '{id}' in {context}.", context);
        }
        void References(IEnumerable<string> values, HashSet<string> allowed, string code, string description, string owner)
        {
            Duplicates(values, owner);
            foreach (var id in values.Where(v => !allowed.Contains(v))) Issue(code, $"'{id}' must reference {description} in this component.", owner);
        }
        void Effects(ProcessingEffects rule, string owner)
        {
            References(rule.Outputs, outputs, "PROCESSING_OUTPUT", "an outgoing broker message", owner);
            References(rule.Calls, calls, "PROCESSING_CALL", "an outbound HTTP call", owner);
            if (rule.Calls.Count > 0) Issue("PROCESSING_HTTP_EFFECTS", "HTTP effects can repeat on retries and are not made atomic by the messaging Outbox. Document idempotency in the condition or recoverability notes.", owner, true);
        }
        void Input(HandlingRule rule, string owner)
        {
            if (!inputs.Contains(rule.Message)) Issue("PROCESSING_INPUT", $"'{rule.Message}' must reference an incoming broker message in this component.", owner);
            handled.Add(rule.Message);
            Effects(rule, owner);
        }
        Duplicates(p.Handlers.Select(h => h.Id).Concat(p.Sagas.Select(s => s.Id)), "handler/saga IDs");
        if (p.SendOnly && (inputs.Count > 0 || p.Handlers.Count > 0 || p.Sagas.Count > 0))
            Issue("PROCESSING_SEND_ONLY", "A send-only NServiceBus endpoint cannot declare consumed messages, handlers or sagas.");
        foreach (var handler in p.Handlers)
        {
            if (handler.Handles.Count == 0) Issue("PROCESSING_EMPTY_HANDLER", "Declare at least one handled message.", handler.Id);
            Duplicates(handler.Handles.Select(h => h.Message), handler.Id);
            foreach (var rule in handler.Handles) Input(rule, handler.Id);
        }
        foreach (var saga in p.Sagas)
        {
            if (saga.Handles.Count + saga.Timeouts.Count == 0) Issue("SAGA_EMPTY", "Declare a handled message or timeout.", saga.Id);
            if (!saga.Handles.Any(h => h.StartsSaga)) Issue("SAGA_NO_START", "No starter is documented. This may describe existing instances only; document how new instances are created.", saga.Id, true);
            if (saga.WhenNotFound is not ("Discard" or "Throw" or "Custom")) Issue("SAGA_NOT_FOUND", "whenNotFound must be Discard, Throw or Custom.", saga.Id);
            if (saga.WhenNotFound == "Custom" && string.IsNullOrWhiteSpace(saga.NotFoundDescription)) Issue("SAGA_NOT_FOUND", "Custom not-found behavior needs notFoundDescription.", saga.Id);
            if (saga.WhenNotFound == "Discard" && saga.Handles.Any(h => !h.StartsSaga))
                Issue("SAGA_LATE_MESSAGE", "A continuation arriving before creation or after completion may be discarded under the declared not-found policy. Review out-of-order delivery.", saga.Id, true);
            if (saga.CorrelationProperty == "Id") Issue("SAGA_INTERNAL_ID", "Do not map the framework's internal saga Id as a business correlation property. Use a business key or SagaId auto-correlation mode.", saga.Id);
            Duplicates(saga.Handles.Select(h => h.Message), saga.Id);
            Duplicates(saga.Timeouts.Select(t => t.Id), saga.Id + " timeouts");
            var timeouts = saga.Timeouts.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
            var requested = new HashSet<string>(StringComparer.Ordinal);
            void Requests(IReadOnlyList<string> ids, bool completes, ProcessingEffects effects, string owner)
            {
                References(ids, timeouts, "SAGA_TIMEOUT_REFERENCE", "a timeout declared by this saga", owner);
                requested.UnionWith(ids);
                if (completes && ids.Count > 0) Issue("SAGA_COMPLETION_TIMEOUT", "This rule may complete the saga and request timeouts. Timeouts for a completed instance will be ignored; review its conditions.", owner, true);
                if (completes && effects.Outputs.Count > 0 && p.Outbox != true)
                    Issue("SAGA_COMPLETION_CONSISTENCY", "Completion and outgoing messages need verified transaction/persistence guarantees or appropriate Outbox support. The catalogue does not verify runtime configuration.", owner, true);
            }
            foreach (var rule in saga.Handles)
            {
                Input(rule, saga.Id);
                var correlation = rule.Correlation;
                if (correlation is null) Issue("SAGA_CORRELATION", "Every saga message must document its correlation mode.", saga.Id);
                else
                {
                    if (correlation.Mode is not ("Property" or "Header" or "Custom" or "SagaId")) Issue("SAGA_CORRELATION", "Unknown saga correlation mode.", saga.Id);
                    if (correlation.Mode is "Property" or "Header" && string.IsNullOrWhiteSpace(saga.CorrelationProperty))
                        Issue("SAGA_CORRELATION", "Property/header mappings require the saga correlationProperty.", saga.Id);
                    if (correlation.Mode == "Property" && string.IsNullOrWhiteSpace(correlation.MessageProperty)) Issue("SAGA_CORRELATION", "Property correlation requires messageProperty.", saga.Id);
                    if (correlation.Mode == "Header" && string.IsNullOrWhiteSpace(correlation.Header)) Issue("SAGA_CORRELATION", "Header correlation requires header.", saga.Id);
                    if (correlation.Mode != "Property" && correlation.MessageProperty is not null || correlation.Mode != "Header" && correlation.Header is not null)
                        Issue("SAGA_CORRELATION", "messageProperty is only valid for Property mode; header is only valid for Header mode.", saga.Id);
                    if (correlation.Mode == "Custom" && string.IsNullOrWhiteSpace(correlation.Description)) Issue("SAGA_CORRELATION", "Describe the custom finder or correlation expression.", saga.Id);
                    if (correlation.Mode == "SagaId" && rule.StartsSaga) Issue("SAGA_CORRELATION", "SagaId auto-correlation describes an existing instance and cannot be the declared creation rule.", saga.Id);
                }
                Requests(rule.RequestsTimeouts, rule.CompletesSaga, rule, saga.Id);
            }
            foreach (var timeout in saga.Timeouts)
            {
                Effects(timeout, saga.Id + "/" + timeout.Id);
                Requests(timeout.RequestsTimeouts, timeout.CompletesSaga, timeout, saga.Id + "/" + timeout.Id);
                if (timeout.Delay is not null && timeout.Schedule is not null) Issue("SAGA_TIMEOUT_SCHEDULE", "Use delay or schedule, not both.", saga.Id);
                if (timeout.Delay is not null)
                {
                    try { if (XmlConvert.ToTimeSpan(timeout.Delay) <= TimeSpan.Zero) throw new FormatException(); }
                    catch (Exception ex) when (ex is FormatException or OverflowException) { Issue("SAGA_TIMEOUT_DELAY", "delay must be a positive ISO 8601 duration, such as PT15M.", saga.Id); }
                }
                if (timeout.Delay is null && timeout.Schedule is null) Issue("SAGA_TIMEOUT_SCHEDULE", "Timeout timing is not documented; specify a delay or describe its dynamic schedule.", saga.Id, true);
            }
            foreach (var timeout in timeouts.Except(requested)) Issue("SAGA_TIMEOUT_UNUSED", $"Timeout '{timeout}' is never requested by a documented rule.", saga.Id, true);
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Queue<string>(saga.Handles.SelectMany(h => h.RequestsTimeouts));
            var byId = saga.Timeouts.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            while (pending.TryDequeue(out var id))
                if (reachable.Add(id) && byId.TryGetValue(id, out var t)) foreach (var next in t.RequestsTimeouts) pending.Enqueue(next);
            foreach (var timeout in timeouts.Except(reachable)) Issue("SAGA_TIMEOUT_UNREACHABLE", $"Timeout '{timeout}' cannot be reached from any documented message handler.", saga.Id, true);
            if (GraphTraversal.HasCycle(timeouts, saga.Timeouts.SelectMany(t => t.RequestsTimeouts.Where(timeouts.Contains).Select(next => (t.Id, next)))))
                Issue("SAGA_TIMEOUT_CYCLE", "Timeouts may schedule a repeated cycle. Confirm state conditions stop it when appropriate; requesting again does not cancel earlier requests.", saga.Id, true);
        }
        if (p.Sagas.Count > 0 && string.IsNullOrWhiteSpace(p.Persistence)) Issue("SAGA_PERSISTENCE_UNSPECIFIED", "Saga persistence is not documented.", warning: true);
        foreach (var duplicate in p.Sagas.Where(s => !string.IsNullOrWhiteSpace(s.DataType)).GroupBy(s => s.DataType, StringComparer.Ordinal).Where(g => g.Count() > 1))
            Issue("SAGA_SHARED_DATA", $"Saga data type '{duplicate.Key}' is shared by several saga declarations. Review NServiceBus startup validation and storage isolation.", warning: true);
        foreach (var input in inputs.Except(handled)) Issue("PROCESSING_UNASSIGNED_INPUT", $"No handler or saga is documented for '{input}'. This may be incomplete documentation.", input, true);
        foreach (var input in handled.GroupBy(i => i, StringComparer.Ordinal).Where(g => g.Count() > 1))
            Issue("PROCESSING_MULTIPLE_HANDLERS", $"'{input.Key}' is handled by multiple processors within the same endpoint. They share delivery/retry behavior; these are not competing broker consumers.", input.Key, true);
        return issues;
    }
}
