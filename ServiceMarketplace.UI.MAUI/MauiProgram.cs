using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.UI.Shared.Auth;
using ServiceMarketplace.UI.Shared.Requests;
using ServiceMarketplace.UI.MAUI.Services;

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

        builder.Services.AddAuthorizationCore();

        builder.Services.AddScoped(_ => new HttpClient
        {
            BaseAddress = new Uri("https://localhost:7147/")
        });

        builder.Services.AddScoped<TokenAuthenticationStateProvider>();
        builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TokenAuthenticationStateProvider>());
        builder.Services.AddScoped<AuthRedirector>();
        builder.Services.AddScoped<AuthState>();

        builder.Services.AddScoped<AuthApiClient>();
        builder.Services.AddScoped<RequestsApiClient>();

        builder.Services.AddScoped<ITokenStorage, MauiTokenStorage>();

        // Keep existing registrations already present in your solution:
        // - ITokenStorage implementation for MAUI

        return builder.Build();
    }
}
