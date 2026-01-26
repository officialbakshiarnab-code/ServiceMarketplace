using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.UI.Shared.Auth;
using ServiceMarketplace.UI.Shared.Configuration;
using ServiceMarketplace.UI.Shared.Requests;
using ServiceMarketplace.UI.MAUI.Services;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace ServiceMarketplace.UI.MAUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        builder.Configuration.AddInMemoryCollection(LoadAppSettings());

        builder.Services.AddAuthorizationCore();

        builder.Services.AddScoped(sp =>
        {
            // NOTE: do not hardcode. MAUI can provide via appsettings / environment.
            var config = sp.GetRequiredService<IConfiguration>();
            var apiBaseUri = ApiBaseUrlResolver.GetApiBaseUri(config);
            return new HttpClient { BaseAddress = apiBaseUri };
        });

        builder.Services.AddScoped<TokenAuthenticationStateProvider>();
        builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TokenAuthenticationStateProvider>());
        builder.Services.AddScoped<AuthRedirector>();
        builder.Services.AddScoped<AuthState>();

        builder.Services.AddScoped<AuthApiClient>();
        builder.Services.AddScoped<RequestsApiClient>();
        builder.Services.AddScoped<BidsApiClient>();

        builder.Services.AddScoped<ITokenStorage, MauiTokenStorage>();

        // Keep existing registrations already present in your solution:
        // - ITokenStorage implementation for MAUI

        return builder.Build();
    }

    private static IEnumerable<KeyValuePair<string, string?>> LoadAppSettings()
    {
        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json").GetAwaiter().GetResult();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var json = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<KeyValuePair<string, string?>>();

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return Array.Empty<KeyValuePair<string, string?>>();

            var list = new List<KeyValuePair<string, string?>>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                list.Add(new KeyValuePair<string, string?>(prop.Name, prop.Value.ToString()));
            }

            return list;
        }
        catch
        {
            return Array.Empty<KeyValuePair<string, string?>>();
        }
    }
}
