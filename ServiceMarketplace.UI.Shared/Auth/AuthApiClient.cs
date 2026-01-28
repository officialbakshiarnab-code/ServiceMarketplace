using System.Net.Http.Json;
using System.Text.Json;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// HTTP client for authentication API endpoints.
/// Handles login/register requests and JWT token persistence.
/// </summary>
public sealed class AuthApiClient(HttpClient httpClient, ITokenStorage tokenStorage)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenStorage _tokenStorage = tokenStorage;

    /// <summary>
    /// Authenticates user and persists JWT token to storage.
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication response with token and metadata</returns>
    /// <exception cref="InvalidOperationException">When login fails or response is invalid</exception>
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/login", request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await TryReadErrorAsync(response, cancellationToken) ?? "Login failed.";
            throw new InvalidOperationException(errorMessage);
        }

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
        
        if (payload is null || string.IsNullOrWhiteSpace(payload.Token))
            throw new InvalidOperationException("Login response was empty.");

        // Persist JWT for future authenticated requests
        await _tokenStorage.SaveTokenAsync(payload.Token);
        
        return payload;
    }

    /// <summary>
    /// Registers a new user account.
    /// </summary>
    /// <param name="request">Registration details including email, password, and role</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="InvalidOperationException">When registration fails</exception>
    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/register", request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await TryReadErrorAsync(response, cancellationToken) ?? "Registration failed.";
            throw new InvalidOperationException(errorMessage);
        }
    }

    /// <summary>
    /// Attempts to extract user-friendly error message from API response.
    /// Handles various error formats: simple strings, validation errors, problem details.
    /// </summary>
    private static async Task<string?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return json;

            // Try common error message properties
            if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                return msg.GetString();

            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                return err.GetString();

            if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                return title.GetString();

            // Handle ASP.NET Core validation errors (ModelState errors)
            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var lines = new List<string>();
                foreach (var prop in errors.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        var messages = prop.Value.EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.String)
                            .Select(e => e.GetString())
                            .Where(s => !string.IsNullOrWhiteSpace(s));

                        var joined = string.Join("; ", messages!);
                        if (!string.IsNullOrWhiteSpace(joined))
                            lines.Add($"{prop.Name}: {joined}");
                    }
                }

                if (lines.Count > 0)
                    return string.Join(" | ", lines);
            }

            // Fallback to raw JSON
            return json;
        }
        catch
        {
            return null;
        }
    }
}