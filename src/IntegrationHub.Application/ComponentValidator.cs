using IntegrationHub.Domain;
namespace IntegrationHub.Application;

public static class ComponentValidator
{
    public static ValidationResult Validate(ComponentDefinition definition)
    {
        var issues = new List<ValidationIssue>();
        void Error(string code, string message, string? binding = null) => issues.Add(new(code, ValidationSeverity.Error, message, NodeId: binding));
        var outgoing = definition.Publishes.Concat(definition.Sends).ToArray();
        var all = definition.Consumes.Concat(outgoing).ToArray();
        var ids = all.Select(b => b.Id).Concat(definition.Endpoints.Select(e => e.Id)).Concat(definition.Calls.Select(c => c.Id)).ToArray();
        foreach (var duplicate in ids.GroupBy(id => id, StringComparer.Ordinal).Where(g => g.Count() > 1))
            Error("DUPLICATE_BINDING_ID", "IDs must be unique across endpoints, calls and message bindings within the component.", duplicate.Key);
        foreach (var duplicate in definition.Endpoints.GroupBy(e => (e.Method, e.Path, e.Version)).Where(g => g.Count() > 1))
            Error("DUPLICATE_ENDPOINT", "This HTTP method, path and version are already exposed by another endpoint.", duplicate.First().Id);
        foreach (var duplicate in definition.Calls.GroupBy(c => (c.TargetComponent, c.Endpoint, c.Method, c.Version)).Where(g => g.Count() > 1))
            Error("DUPLICATE_CALL", "This HTTP call is declared more than once.", duplicate.First().Id);
        foreach (var binding in definition.Sends.Where(b => b.MessageType != MessageType.Command || b.Channel.Kind == ChannelKind.Http))
            Error("COMMAND_REQUIRED", "sends declares broker commands. Use calls for HTTP and publishes for events.", binding.Id);
        foreach (var binding in all.Where(b => b.Channel.Kind == ChannelKind.Http))
            issues.Add(new("LEGACY_HTTP_BINDING", ValidationSeverity.Warning, "Legacy HTTP declaration: use endpoints for exposed APIs and calls for callers. Its original matching rules remain supported; no HTTP method is inferred.", NodeId: binding.Id));
        foreach (var binding in definition.Publishes.Where(b => b.MessageType == MessageType.Command && b.Channel.Kind != ChannelKind.Http))
            issues.Add(new("LEGACY_COMMAND_PUBLICATION", ValidationSeverity.Warning, "Move this command from publishes to sends. Existing matching is preserved.", NodeId: binding.Id));
        foreach (var (bindings, consumes) in new[] { (definition.Consumes, true), ((IReadOnlyList<MessageBinding>)outgoing, false) })
        {
            // Repeating a declaration must not multiply inferred edges.
            foreach (var duplicate in bindings.GroupBy(b => (b.Contract, b.Version, b.MessageType, b.Channel, b.SourceComponent, b.TargetComponent, b.Subscription)).Where(g => g.Count() > 1))
                Error("DUPLICATE_BINDING", "This message route is declared more than once.", duplicate.First().Id);
            foreach (var binding in bindings)
            {
                if (consumes && binding.TargetComponent is not null || !consumes && binding.SourceComponent is not null)
                    Error("BINDING_DIRECTION", "sourceComponent is only valid on consumes; targetComponent is only valid on publishes.", binding.Id);
                if (binding.TargetComponent is not null && binding.Channel.Kind != ChannelKind.Http)
                    Error("TARGET_CHANNEL", "targetComponent is supported only for direct HTTP routes; broker delivery is determined by the channel and subscription.", binding.Id);
                if (consumes && binding.Channel.Kind is ChannelKind.Topic or ChannelKind.Stream && string.IsNullOrWhiteSpace(binding.Subscription))
                    Error("SUBSCRIPTION_REQUIRED", "Topic consumers need a subscription; stream consumers need a consumer group in subscription.", binding.Id);
                if (binding.Subscription is not null && (!consumes || binding.Channel.Kind is not (ChannelKind.Topic or ChannelKind.Stream)))
                    Error("SUBSCRIPTION_CHANNEL", "subscription is only valid on Topic or Stream consumers.", binding.Id);
            }
        }
        if (ids.Length == 0) issues.Add(new("NO_BINDINGS", ValidationSeverity.Warning, "This component has no HTTP or messaging declarations and will remain unlinked."));
        return new(issues);
    }
}
