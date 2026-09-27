namespace IntegrationHub.Web;

public static class ComponentVisuals
{
    public static string Label(string type) => type switch
    {
        "Api" => "API", "ApiManagement" => "API management", "AzureFunction" => "Azure Function",
        "LogicApp" => "Logic App", "ServiceBusTopic" => "Service Bus topic", "ServiceBusQueue" => "Service Bus queue",
        "InternalSystem" => "Internal system", "ExternalSystem" => "External system", "EventGrid" => "Event Grid",
        "MessageBroker" => "Message broker", "NuGetPackage" => "NuGet package", "FileShare" => "File share",
        "ManualProcess" => "Manual process", "Sftp" => "SFTP", _ => type
    };
    public static string Category(string type) => type switch
    {
        "Api" or "ApiManagement" => "api",
        "AzureFunction" => "function",
        "LogicApp" or "Transformation" or "Library" or "NuGetPackage" => "compute",
        "ServiceBusTopic" or "ServiceBusQueue" or "MessageBroker" or "EventGrid" => "messaging",
        "Database" or "Storage" or "Sftp" or "FileShare" => "data",
        "InternalSystem" or "ExternalSystem" or "SaaS" => "system",
        _ => "other"
    };
    public static string Icon(string type) => type switch
    {
        "Api" => HubIcons.Code,
        "ApiManagement" => HubIcons.Gateway,
        "AzureFunction" => HubIcons.Bolt,
        "LogicApp" => HubIcons.LogicApp,
        "ServiceBusTopic" => HubIcons.Topic,
        "ServiceBusQueue" => HubIcons.Queue,
        "Database" => HubIcons.Database,
        "Storage" => HubIcons.Storage,
        "InternalSystem" => HubIcons.InternalSystem,
        "ExternalSystem" => HubIcons.ExternalSystem,
        "SaaS" => HubIcons.CloudQueue,
        "EventGrid" => HubIcons.Event,
        "MessageBroker" => HubIcons.Hub,
        "Transformation" => HubIcons.Transform,
        "Library" => HubIcons.Library,
        "NuGetPackage" => HubIcons.Package,
        "Sftp" => HubIcons.Sftp,
        "FileShare" => HubIcons.FileShare,
        "ManualProcess" => HubIcons.Person,
        _ => HubIcons.Custom
    };
    public static string OrUnspecified(string value) => string.IsNullOrWhiteSpace(value) ? "Not specified" : value;
}
