using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using ServiceMarketplace.UI.Shared.Admin;
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

// Configure logging (required for AuthApiClient ILogger)
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Resolve API base URL from configuration (required)
var apiBaseUri = ApiBaseUrlResolver.GetApiBaseUri(builder.Configuration);

// Configure HTTP client handler that automatically attaches JWT tokens
// This handler intercepts every HTTP request and:
// 1. Reads stored JWT token from LocalStorage
// 2. Attaches it as Bearer authorization header
// 3. Sends request to API
// 4. Returns response
// This eliminates need for manual token attachment in each API client
builder.Services.AddScoped<AuthorizingHttpClientHandler>();

// Configure HTTP client for API calls with platform identification
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthorizingHttpClientHandler>();
    handler.InnerHandler = new HttpClientHandler();
    
    // Create HttpClient with token handler in front of the platform HTTP transport.
    // Token handler will be called before request is sent to server
    var httpClient = new HttpClient(handler)
    {
        BaseAddress = apiBaseUri,
        Timeout = TimeSpan.FromSeconds(30)  // 30-second timeout for all requests
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

// Configure new auth state management services
builder.Services.AddScoped<AuthenticationStateInitializer>();
builder.Services.AddScoped<SafeLogoutService>();

// Configure token refresh HTTP client for automatic token refresh on 401
builder.Services.AddScoped<TokenRefreshHttpClient>();

// Configure API clients
builder.Services.AddScoped<AuthApiClient>();
builder.Services.AddScoped<RequestsApiClient>();
builder.Services.AddScoped<ServiceCatalogApiClient>();
builder.Services.AddScoped<ProductCatalogApiClient>();
builder.Services.AddScoped<ServiceOrdersApiClient>();
builder.Services.AddScoped<ServiceOrderMessagesApiClient>();
builder.Services.AddScoped<ServiceOrderPaymentsApiClient>();
builder.Services.AddScoped<ServiceOrderReviewsApiClient>();
builder.Services.AddScoped<ServicePackagesApiClient>();
builder.Services.AddScoped<NotificationsApiClient>();
builder.Services.AddScoped<BidsApiClient>();
builder.Services.AddScoped<ProviderApplicationsApiClient>();
builder.Services.AddScoped<SellerApplicationsApiClient>();
builder.Services.AddScoped<ProductListingsApiClient>();
builder.Services.AddScoped<ProductDeliveryOrdersApiClient>();
builder.Services.AddScoped<MarketplaceSearchApiClient>();
builder.Services.AddScoped<ProfilesApiClient>();
builder.Services.AddScoped<ContactRequestsApiClient>();
builder.Services.AddScoped<AuditLogsApiClient>();
builder.Services.AddScoped<AdminKpiApiClient>();

// Configure platform-specific services
builder.Services.AddScoped<ITokenStorage, WebSessionTokenStorage>();

// Role validation services (UI.Shared)
builder.Services.AddScoped<RoleValidator>();

var host = builder.Build();

var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
await jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_token");
await jsRuntime.InvokeVoidAsync("localStorage.removeItem", "refresh_token");
await jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "auth_token");
await jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "refresh_token");

// INITIALIZATION: Restore authentication state for the current app session
// This happens before rendering any components, so:
// 1. Current in-memory JWT token is read if the app session already has one
// 2. Token is validated and parsed
// 3. AuthenticationStateProvider notified with restored claims
// 4. <AuthorizeView> and <AuthorizeRouteView> components get correct auth state
// 5. Browser close, reload, or debug restart starts signed out
var authStateInitializer = host.Services.GetRequiredService<AuthenticationStateInitializer>();
var authStateProvider = host.Services.GetRequiredService<TokenAuthenticationStateProvider>();

// Initialize auth state by notifying the provider
await authStateInitializer.InitializeAsync(async () =>
{
    var tokenStorage = host.Services.GetRequiredService<ITokenStorage>();
    var token = await tokenStorage.GetTokenAsync();
    
    if (!string.IsNullOrWhiteSpace(token))
    {
        // Token exists - notify provider to parse and validate it
        authStateProvider.NotifyAuthenticationStateChanged();
    }
});

await host.RunAsync();

