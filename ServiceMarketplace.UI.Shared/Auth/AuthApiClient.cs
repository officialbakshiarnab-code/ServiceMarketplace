using System.Net.Http.Json;
using System.Text.Json;

namespace ServiceMarketplace.UI.Shared.Auth;

public sealed class AuthApiClient(HttpClient httpClient, ITokenStorage tokenStorage)
{
    // Purpose: Auth API client for Login/Register calls.
    // Uses ApiBaseUrl-configured HttpClient and persists JWT to ITokenStorage.
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenStorage _tokenStorage = tokenStorage;

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/login", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response, cancellationToken) ?? "Login failed.");

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Token))
            throw new InvalidOperationException("Login response was empty.");

        await _tokenStorage.SaveTokenAsync(payload.Token);
        return payload;
    }

    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/register", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response, cancellationToken) ?? "Registration failed.");
    }

    private static async Task<string?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                    return msg.GetString();

                if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                    return err.GetString();

                if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                    return title.GetString();

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
            }

            return json;
        }
        catch
        {
            return null;
        }
    }
}