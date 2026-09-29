using IntegrationHub.Api;
using IntegrationHub.Api.Security;
using IntegrationHub.Contracts;
using IntegrationHub.Infrastructure;
using IntegrationHub.Infrastructure.Persistence;
using IntegrationHub.Web;
using IntegrationHub.Web.Components;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 6_100_000);
var databaseOptions = builder.Configuration.GetSection("Database").Get<DatabaseOptions>() ?? new();
databaseOptions.Validate(builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"));
var connectionString = databaseOptions.ResolveConnectionString(builder.Configuration.GetConnectionString("IntegrationHub") ?? "", builder.Environment.ContentRootPath);
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("Database"));
builder.Services.AddIntegrationHub(connectionString, databaseOptions.Provider);
builder.Services.AddHubSecurity(builder.Configuration, builder.Environment);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(o => HubJson.Configure(o.SerializerOptions));
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddHealthChecks().AddCheck<HubReadinessCheck>("catalogue-storage", tags: ["ready"]);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => { o.SwaggerDoc("v1", new() { Title = "Integration Hub API", Version = "v1", Description = "Define once. Derive architecture, documentation, catalogues and impact analysis." }); });
// Registers the framework file-version service used by the stylesheet links.
builder.Services.AddMvcCore().AddRazorViewEngine();
builder.Services.AddRazorComponents().AddInteractiveServerComponents(o => o.DetailedErrors = builder.Environment.IsDevelopment()).AddHubOptions(o => o.MaximumReceiveMessageSize = 8 * 1024 * 1024);
builder.Services.AddMudServices();
builder.Services.AddScoped<HubApiClient>();
builder.Services.AddCascadingAuthenticationState();
var app = builder.Build();
var migrateOnly = args.Contains("--migrate", StringComparer.Ordinal);
if (migrateOnly || databaseOptions.InitializeSqliteOnStartup)
{
    using var scope = app.Services.CreateScope();
    await DatabaseInitialization.ApplyMigrationsAsync(scope.ServiceProvider.GetRequiredService<HubDbContext>());
    app.Logger.LogInformation("Catalogue migrations applied for {DatabaseProvider}", databaseOptions.Provider);
}
if (migrateOnly) return;
app.UseExceptionHandler();
app.UseStatusCodePages(async status =>
{
    if (status.HttpContext.Request.Path.StartsWithSegments("/api"))
        await Results.Problem(statusCode: status.HttpContext.Response.StatusCode).ExecuteAsync(status.HttpContext);
});
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "no-store";
        if (context.User.Identity?.IsAuthenticated == true && context.Request.Method is "POST" or "PUT" or "DELETE" or "PATCH" && !context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { await Results.Problem(statusCode: 400, title: "Invalid CSRF token", detail: "Reload the page before submitting changes.").ExecuteAsync(context); return; }
        }
    }
    await next(context);
});
app.MapGet("/auth/login", () => Results.Challenge(new AuthenticationProperties { RedirectUri = "/" }, [OpenIdConnectDefaults.AuthenticationScheme]));
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = h => h.Tags.Contains("ready") }).AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapSwagger().RequireAuthorization("Viewer");
app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "Integration Hub v1"));
app.MapIntegrationEndpoints();
app.MapComponentEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode().RequireAuthorization("Viewer");
app.Run();
public partial class Program;
