using System.Net.Http.Json;
using ServiceMarketplace.UI.Shared.Auth;

namespace ServiceMarketplace.UI.Shared.Services;

public class AuthApiClient
{
    private readonly HttpClient _http;
    private readonly ITokenStorage _tokenStorage;

    public AuthApiClient(HttpClient http, ITokenStorage tokenStorage)
    {
        _http = http;
        _tokenStorage = tokenStorage;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", request);
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>()
                   ?? throw new Exception("Invalid auth response");

        await _tokenStorage.SaveTokenAsync(auth.Token);
        return auth;
    }
}
