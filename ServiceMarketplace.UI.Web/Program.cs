using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ServiceMarketplace.UI.Shared.Auth;
using ServiceMarketplace.UI.Shared.Configuration;
using ServiceMarketplace.UI.Shared.Requests;
using ServiceMarketplace.UI.Web.Services;
using ServiceMarketplace.UI.Web;

// Purpose: Blazor WebAssembly host.
// - UI.Shared provides shared Razor components, DTOs, and auth logic.
// - Configuration (ApiBaseUrl) is loaded from appsettings.json.
// - HttpClient uses ApiBaseUrl; never hardcode URLs or tokens.

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUri = ApiBaseUrlResolver.GetApiBaseUri(builder.Configuration);

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = apiBaseUri
});

builder.Services.AddAuthorizationCore();

builder.Services.AddScoped<TokenAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TokenAuthenticationStateProvider>());
builder.Services.AddScoped<AuthRedirector>();
builder.Services.AddScoped<AuthState>();

builder.Services.AddScoped<AuthApiClient>();
builder.Services.AddScoped<RequestsApiClient>();
builder.Services.AddScoped<BidsApiClient>();

builder.Services.AddScoped<ITokenStorage, LocalStorageTokenStorage>();

await builder.Build().RunAsync();
