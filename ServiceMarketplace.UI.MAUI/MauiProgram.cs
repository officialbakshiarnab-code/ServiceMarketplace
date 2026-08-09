using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.UI.Shared.Auth;
using ServiceMarketplace.UI.Shared.Configuration;
using ServiceMarketplace.UI.Shared.Requests;
using ServiceMarketplace.UI.MAUI.Services;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

// ========================================
// MAUI Blazor Hybrid Host Configuration
// ========================================
// Purpose: Configures dependency injection, authentication, and API client for MAUI Hybrid UI.
// Architecture:
// - Loads appsettings.json from app package for ApiBaseUrl
// - Uses UI.Shared for shared Razor components, auth logic, and API clients
// - HttpClient always uses ApiBaseUrl; never hardcode URLs or tokens
// - Android emulator: localhost → 10.0.2.2 mapping handled automatically

namespace ServiceMarketplace.UI.MAUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        // Configure MAUI app
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Configure Blazor WebView
        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Load configuration from bundled appsettings.json
        builder.Configuration.AddInMemoryCollection(LoadAppSettings());

        // Configure authorization
        builder.Services.AddAuthorizationCore();

        // Configure HTTP client handler that automatically attaches JWT tokens
        builder.Services.AddScoped<AuthorizingHttpClientHandler>();

        // Configure HTTP client for API calls with platform identification
        builder.Services.AddScoped(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var apiBaseUri = ApiBaseUrlResolver.GetApiBaseUri(config);
            var handler = sp.GetRequiredService<AuthorizingHttpClientHandler>();
            handler.InnerHandler = new HttpClientHandler();
            
            var httpClient = new HttpClient(handler)
            { 
                BaseAddress = apiBaseUri,
                Timeout = TimeSpan.FromSeconds(30)
            };
            
            // Add platform header for audit logging
#if ANDROID
            httpClient.DefaultRequestHeaders.Add("X-Platform", "MAUI-Android");
#elif IOS
            httpClient.DefaultRequestHeaders.Add("X-Platform", "MAUI-iOS");
#elif WINDOWS
            httpClient.DefaultRequestHeaders.Add("X-Platform", "MAUI-Windows");
#elif MACCATALYST
            httpClient.DefaultRequestHeaders.Add("X-Platform", "MAUI-MacCatalyst");
#else
            httpClient.DefaultRequestHeaders.Add("X-Platform", "MAUI-Unknown");
#endif
            
            return httpClient;
        });

        // Configure authentication services
        builder.Services.AddScoped<TokenAuthenticationStateProvider>();
        builder.Services.AddScoped<AuthenticationStateProvider>(sp => 
            sp.GetRequiredService<TokenAuthenticationStateProvider>());
        builder.Services.AddScoped<AuthRedirector>();
        builder.Services.AddScoped<AuthState>();

        // Configure auth state initialization
        builder.Services.AddScoped<AuthenticationStateInitializer>();
        builder.Services.AddScoped<SafeLogoutService>();

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

        // Configure platform-specific services
        builder.Services.AddScoped<ITokenStorage, MauiTokenStorage>();

        // Build app
        var app = builder.Build();

        // INITIALIZATION: Initialize auth state in the background
        // This happens asynchronously without blocking app startup
        _ = Task.Run(async () =>
        {
            try
            {
                var authStateInitializer = app.Services.GetRequiredService<AuthenticationStateInitializer>();
                var authStateProvider = app.Services.GetRequiredService<TokenAuthenticationStateProvider>();

                await authStateInitializer.InitializeAsync(async () =>
                {
                    var tokenStorage = app.Services.GetRequiredService<ITokenStorage>();
                    var token = await tokenStorage.GetTokenAsync();
                    
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        // Token exists - notify provider to parse and validate it
                        authStateProvider.NotifyAuthenticationStateChanged();
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MauiProgram] Error initializing auth state: {ex.Message}");
            }
        });

        return app;
    }

    /// <summary>
    /// Loads configuration from appsettings.json bundled in the MAUI app package.
    /// Falls back to empty collection if file is missing or invalid.
    /// </summary>
    /// <returns>Configuration key-value pairs from appsettings.json</returns>
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

            var settings = new List<KeyValuePair<string, string?>>();
            
            // Flatten JSON object to configuration key-value pairs
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var value = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : property.Value.ToString();
                    
                settings.Add(new KeyValuePair<string, string?>(property.Name, value));
            }

            return settings;
        }
        catch (Exception ex)
        {
            // Log warning but don't crash - configuration validation happens later
            System.Diagnostics.Debug.WriteLine($"Warning: Failed to load appsettings.json: {ex.Message}");
            return Array.Empty<KeyValuePair<string, string?>>();
        }
    }
}

