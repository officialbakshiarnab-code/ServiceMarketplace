using Microsoft.Extensions.Configuration;

namespace ServiceMarketplace.UI.Shared.Configuration;

public static class ApiBaseUrlResolver
{
    public static Uri GetApiBaseUri(IConfiguration configuration)
    {
        var raw = configuration["ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("Missing configuration value 'ApiBaseUrl' for API host base URL.");

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Configuration value 'ApiBaseUrl' must be an absolute URI.");

#if ANDROID
        // Android emulator cannot reach host machine via 'localhost' inside the emulator.
        // Map localhost to 10.0.2.2 when user configured localhost.
        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            uri = new UriBuilder(uri) { Host = "10.0.2.2" }.Uri;
#endif

        return uri;
    }
}
