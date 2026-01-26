using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ServiceMarketplace.UI.Shared.Auth;
using ServiceMarketplace.UI.Shared.Requests;
using ServiceMarketplace.UI.Web.Services;
using ServiceMarketplace.UI.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddress = builder.Configuration["ApiBaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseAddress))
    apiBaseAddress = builder.HostEnvironment.BaseAddress;

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(apiBaseAddress, UriKind.Absolute)
});

builder.Services.AddAuthorizationCore();

builder.Services.AddScoped<TokenAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TokenAuthenticationStateProvider>());
builder.Services.AddScoped<AuthRedirector>();
builder.Services.AddScoped<AuthState>();

builder.Services.AddScoped<AuthApiClient>();
builder.Services.AddScoped<RequestsApiClient>();

builder.Services.AddScoped<ITokenStorage, LocalStorageTokenStorage>();

await builder.Build().RunAsync();
