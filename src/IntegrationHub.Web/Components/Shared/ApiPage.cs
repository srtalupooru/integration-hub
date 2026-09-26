using Microsoft.AspNetCore.Components;
namespace IntegrationHub.Web.Components.Shared;
public abstract class ApiPage : ComponentBase
{
    [Inject] protected HubApiClient Api { get; set; } = default!;
    protected string? Error { get; set; }
    protected bool Loading { get; set; } = true;
    private bool _ready;
    protected override Task OnParametersSetAsync() => _ready ? ExecuteAsync(LoadAsync) : Task.CompletedTask;
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        await ExecuteAsync(async () => { await Api.InitialiseAsync(); await LoadAsync(); });
        _ready = true;
    }
    protected abstract Task LoadAsync();
    protected async Task ExecuteAsync(Func<Task> action)
    {
        Error = null; Loading = true;
        try { await action(); }
        catch (HubApiException ex) { Error = ex.Message; }
        catch (Microsoft.JSInterop.JSException) { Error = "The request could not reach the application. Check your connection and retry."; }
        finally { Loading = false; StateHasChanged(); }
    }
}
