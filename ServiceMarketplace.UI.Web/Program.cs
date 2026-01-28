using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ServiceMarketplace.UI.Shared.Auth;
using ServiceMarketplace.UI.Shared.Configuration;
using ServiceMarketplace.UI.Shared.Requests;
using ServiceMarketplace.UI.Web.Services;
using ServiceMarketplace.UI.Web;

// ========================================
// Blazor WebAssembly Host Configuration
// ========================================
// Purpose: Configures dependency injection, authentication, and API client for the Web UI.
// Architecture:
// - UI.Shared provides shared Razor components, DTOs, and auth logic
// - Configuration (ApiBaseUrl) is loaded from wwwroot/appsettings.json
// - HttpClient always uses ApiBaseUrl; never hardcode URLs or tokens

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Configure root components
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Resolve API base URL from configuration (required)
var apiBaseUri = ApiBaseUrlResolver.GetApiBaseUri(builder.Configuration);

// Configure HTTP client for API calls with platform identification
builder.Services.AddScoped(sp =>
{
    var httpClient = new HttpClient
    {
        BaseAddress = apiBaseUri
    };
    
    // Add platform header for audit logging
    httpClient.DefaultRequestHeaders.Add("X-Platform", "Web");
    
    return httpClient;
});

// Configure authorization
builder.Services.AddAuthorizationCore();

// Configure authentication services
builder.Services.AddScoped<TokenAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => 
    sp.GetRequiredService<TokenAuthenticationStateProvider>());
builder.Services.AddScoped<AuthRedirector>();
builder.Services.AddScoped<AuthState>();

// Configure API clients
builder.Services.AddScoped<AuthApiClient>();
builder.Services.AddScoped<RequestsApiClient>();
builder.Services.AddScoped<BidsApiClient>();

// Configure platform-specific services
builder.Services.AddScoped<ITokenStorage, LocalStorageTokenStorage>();

await builder.Build().RunAsync();
