using ServiceMarketplace.UI.Shared.Auth;

namespace ServiceMarketplace.UI.Web.Services;

// Purpose: Web token storage for the current Blazor app session only.
// Tokens are intentionally not persisted in localStorage/sessionStorage.
public sealed class WebSessionTokenStorage : ITokenStorage
{
    private string? _accessToken;
    private string? _refreshToken;

    public Task SaveTokenAsync(string token)
    {
        _accessToken = token;
        return Task.CompletedTask;
    }

    public Task<string?> GetTokenAsync()
        => Task.FromResult(_accessToken);

    public Task SaveRefreshTokenAsync(string refreshToken)
    {
        _refreshToken = refreshToken;
        return Task.CompletedTask;
    }

    public Task<string?> GetRefreshTokenAsync()
        => Task.FromResult(_refreshToken);

    public Task ClearAsync()
    {
        _accessToken = null;
        _refreshToken = null;
        return Task.CompletedTask;
    }
}
