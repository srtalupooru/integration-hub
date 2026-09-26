using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
namespace IntegrationHub.Api.Security;

public sealed class HubAuthenticationOptions
{
    public string Mode { get; set; } = "Entra";
    public string Authority { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Audience { get; set; } = "";
    public string DevelopmentRole { get; set; } = "Admin";
}
public static class HubSecurity
{
    public static void AddHubSecurity(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.GetSection("Authentication").Get<HubAuthenticationOptions>() ?? new();
        services.Configure<HubAuthenticationOptions>(configuration.GetSection("Authentication"));
        var authentication = services.AddAuthentication(o => { o.DefaultScheme = "Hub"; o.DefaultChallengeScheme = "Hub"; });
        if (options.Mode == "Development")
        {
            if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing")) throw new InvalidOperationException("Development authentication is only allowed in Development or Testing.");
            if (options.DevelopmentRole is not ("Viewer" or "Editor" or "Admin")) throw new InvalidOperationException("Invalid development role.");
            authentication.AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>("Hub", _ => { });
        }
        else
        {
            if (options.Mode != "Entra" || !Uri.TryCreate(options.Authority, UriKind.Absolute, out var authority) || authority.Scheme != "https" || string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.Audience)) throw new InvalidOperationException("Configure Authentication Authority, ClientId and Audience for Entra ID.");
            authentication.AddPolicyScheme("Hub", "Cookie or Bearer", o => o.ForwardDefaultSelector = ctx => ctx.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? JwtBearerDefaults.AuthenticationScheme : CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(o =>
                {
                    o.Cookie.HttpOnly = true; o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.SameSite = SameSiteMode.Lax;
                    o.Events.OnRedirectToLogin = context => { if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = 401; else context.Response.Redirect("/auth/login"); return Task.CompletedTask; };
                    o.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
                })
                .AddOpenIdConnect(o =>
                {
                    o.Authority = options.Authority; o.ClientId = options.ClientId; o.ClientSecret = options.ClientSecret;
                    o.ResponseType = "code"; o.UsePkce = true; o.SaveTokens = false; o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    o.MapInboundClaims = false; o.TokenValidationParameters.NameClaimType = "name"; o.TokenValidationParameters.RoleClaimType = "roles";
                })
                .AddJwtBearer(o =>
                {
                    o.Authority = options.Authority; o.Audience = options.Audience; o.MapInboundClaims = false;
                    o.TokenValidationParameters.NameClaimType = "name"; o.TokenValidationParameters.RoleClaimType = "roles";
                });
        }
        services.AddAuthorization(o =>
        {
            o.AddPolicy("Viewer", p => p.RequireAuthenticatedUser().RequireRole("Viewer", "Editor", "Admin"));
            o.AddPolicy("Editor", p => p.RequireAuthenticatedUser().RequireRole("Editor", "Admin"));
            o.AddPolicy("Admin", p => p.RequireAuthenticatedUser().RequireRole("Admin"));
        });
    }
}
public sealed class DevelopmentAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IOptions<HubAuthenticationOptions> hubOptions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "Local developer"), new Claim(ClaimTypes.NameIdentifier, "local-development"), new Claim(ClaimTypes.Role, hubOptions.Value.DevelopmentRole)], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
