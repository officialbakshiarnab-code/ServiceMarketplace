using System.Net.Http.Json;
using ServiceMarketplace.UI.Shared.Services;

namespace ServiceMarketplace.UI.Shared.Auth;

public sealed class AuthApiClient(HttpClient httpClient, ITokenStorage tokenStorage)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenStorage _tokenStorage = tokenStorage;

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/login", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Token))
            throw new InvalidOperationException("Login response was empty.");

        await _tokenStorage.SaveTokenAsync(payload.Token);
        return payload;
    }

    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/register", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}