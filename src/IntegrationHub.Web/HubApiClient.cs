using System.Text.Json;
using IntegrationHub.Contracts;
using Microsoft.JSInterop;
namespace IntegrationHub.Web;

// Browser requests preserve the authenticated same-origin cookie; no server-side impersonation or shared tokens.
public sealed class HubApiClient(IJSRuntime js)
{
    public SessionInfo? Session { get; private set; }
    public bool CanEdit => Session?.Roles.Any(r => r is "Editor" or "Admin") == true;
    public bool IsAdmin => Session?.Roles.Contains("Admin") == true;
    private Task? _initialization;
    public Task InitialiseAsync() => _initialization ??= InitialiseCoreAsync();
    private async Task InitialiseCoreAsync() => Session = await GetAsync<SessionInfo>("/api/session");
    public Task<T> GetAsync<T>(string path) => SendAsync<T>("GET", path, null);
    public async Task<T> SendAsync<T>(string method, string path, object? body)
    {
        var response = await js.InvokeAsync<ApiResponse>("hub.request", method, path, body is null ? null : JsonSerializer.Serialize(body, HubJson.Options), Session?.CsrfToken);
        if (response.Status >= 400)
        {
            string message = $"Request failed ({response.Status}).";
            try
            {
                using var doc = JsonDocument.Parse(response.Body);
                if (doc.RootElement.TryGetProperty("detail", out var detail)) message = detail.GetString() ?? message;
                else if (doc.RootElement.TryGetProperty("title", out var title)) message = title.GetString() ?? message;
            }
            catch (JsonException) { }
            throw new HubApiException(response.Status, message);
        }
        if (typeof(T) == typeof(string)) return (T)(object)response.Body;
        return response.Status == 204 ? default! : JsonSerializer.Deserialize<T>(response.Body, HubJson.Options)!;
    }
    public sealed record ApiResponse(int Status, string Body);
}
public sealed class HubApiException(int status, string message) : Exception(message) { public int Status { get; } = status; }
