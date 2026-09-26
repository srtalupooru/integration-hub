using MudBlazor;
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
        "AzureFunction" or "LogicApp" or "Transformation" => "compute",
        "ServiceBusTopic" or "ServiceBusQueue" or "MessageBroker" or "EventGrid" => "messaging",
        "Database" or "Storage" or "Sftp" or "FileShare" => "data",
        _ => "system"
    };
    public static string Icon(string type) => Category(type) switch
    {
        "api" => Icons.Material.Outlined.Code,
        "compute" => Icons.Material.Outlined.Bolt,
        "messaging" => Icons.Material.Outlined.Forum,
        "data" => Icons.Material.Outlined.Storage,
        _ => Icons.Material.Outlined.Widgets
    };
    public static string OrUnspecified(string value) => string.IsNullOrWhiteSpace(value) ? "Not specified" : value;
}
