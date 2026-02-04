namespace ServiceMarketplace.API.Configuration;

/// <summary>
/// CORS configuration settings.
/// Defines which origins are allowed to make cross-origin requests to the API.
/// </summary>
public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>
    /// List of allowed origins for CORS requests.
    /// Should not contain wildcards in production.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>
    /// Validates the CORS settings.
    /// </summary>
    public void Validate()
    {
        if (AllowedOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "No CORS origins configured. Add 'Cors:AllowedOrigins' array to appsettings.json");
        }

        foreach (var origin in AllowedOrigins)
        {
            if (string.IsNullOrWhiteSpace(origin))
            {
                throw new InvalidOperationException(
                    "CORS origin cannot be null or whitespace");
            }

            if (origin.Contains('*') && !IsLocalhost(origin))
            {
                throw new InvalidOperationException(
                    $"Wildcard CORS origin '{origin}' is not allowed in production. Specify exact origins.");
            }
        }
    }

    private static bool IsLocalhost(string origin)
    {
        return origin.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
               origin.Contains("127.0.0.1");
    }
}

/// <summary>
/// Security configuration settings for HTTPS and security headers.
/// </summary>
public sealed class SecuritySettings
{
    public const string SectionName = "Security";

    /// <summary>
    /// Whether to enforce HTTPS redirection for all HTTP requests.
    /// Should be true in production.
    /// </summary>
    public bool EnforceHttps { get; set; }

    /// <summary>
    /// Whether to use HTTP Strict Transport Security (HSTS).
    /// Should be true in production.
    /// </summary>
    public bool UseHsts { get; set; }

    /// <summary>
    /// HSTS max-age in seconds. Default is 1 year (31536000 seconds).
    /// </summary>
    public int HstsMaxAgeSeconds { get; set; } = 31536000;
}
