using Microsoft.Extensions.Configuration;

namespace ServiceMarketplace.UI.Shared.Configuration;

/// <summary>
/// Resolves API base URL from configuration with platform-specific adjustments.
/// Handles Android emulator localhost mapping automatically.
/// </summary>
public static class ApiBaseUrlResolver
{
    private const string ConfigKey = "ApiBaseUrl";
    private const string AndroidEmulatorHost = "10.0.2.2";

    /// <summary>
    /// Retrieves and validates API base URI from configuration.
    /// On Android, automatically maps localhost to 10.0.2.2 for emulator access.
    /// </summary>
    /// <param name="configuration">Application configuration containing ApiBaseUrl</param>
    /// <returns>Validated absolute URI for API calls</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when ApiBaseUrl is missing, empty, or not a valid absolute URI
    /// </exception>
    public static Uri GetApiBaseUri(IConfiguration configuration)
    {
        var raw = configuration[ConfigKey];
        
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                $"Missing configuration value '{ConfigKey}' for API host base URL. " +
                $"Ensure appsettings.json contains a valid '{ConfigKey}' entry.");
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(
                $"Configuration value '{ConfigKey}' must be an absolute URI. " +
                $"Current value: '{raw}'");
        }

#if ANDROID
        // Android emulator cannot reach the host machine via 'localhost'.
        // The special IP 10.0.2.2 maps to the host's loopback interface.
        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            uri = new UriBuilder(uri) { Host = AndroidEmulatorHost }.Uri;
        }
#endif

        return uri;
    }
}
